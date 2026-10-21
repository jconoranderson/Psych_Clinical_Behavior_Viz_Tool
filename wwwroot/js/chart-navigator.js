(function () {
    const epochMilliseconds = Date.UTC(1970, 0, 1);
    const dayMilliseconds = 24 * 60 * 60 * 1000;
    let suppressChartZoomEventsUntil = 0;
    let dashboardReference = null;
    let resetHandlerInstalled = false;
    let liveZoomInFlight = false;
    let pendingLiveZoom = null;
    let monthlyAxisResizeInstalled = false;

    function formatDate(day) {
        return new Date(epochMilliseconds + day * dayMilliseconds)
            .toISOString()
            .slice(0, 10);
    }

    function updateMonthlyNavigatorWindow(retries = 4) {
        const control = document.getElementById("navigator-window-control");
        if (!control || control.dataset.period !== "Month") return;
        const selection = document.querySelector(".navigator-selection-window");
        const line = selection?.parentElement?.querySelector(
            ".apexcharts-line-series .apexcharts-line");
        const startInput = document.getElementById("chart-range-start");
        if (!selection || !startInput?.value) return;
        if (!line || !line.getBoundingClientRect().width) {
            if (retries > 0) setTimeout(() => updateMonthlyNavigatorWindow(retries - 1), 60);
            return;
        }

        const firstMonthIndex = Number(control.dataset.firstMonthIndex);
        const monthCount = Number(control.dataset.monthCount);
        const periodCount = Math.min(monthCount,
            Math.max(1, Number(control.dataset.periodCount)));
        if (!Number.isFinite(firstMonthIndex) || monthCount < 1) return;
        const startDate = new Date(`${startInput.value}T00:00:00Z`);
        const monthIndex = Math.max(0, Math.min(monthCount - periodCount,
            startDate.getUTCFullYear() * 12 + startDate.getUTCMonth() - firstMonthIndex));
        const bounds = line.getBoundingClientRect();
        const parentBounds = selection.parentElement.getBoundingClientRect();
        const step = monthCount > 1 ? bounds.width / (monthCount - 1) : bounds.width;
        const left = bounds.left - parentBounds.left + (monthIndex - 0.5) * step;
        const width = periodCount * step;
        selection.style.left = `${left}px`;
        selection.style.width = `${width}px`;
        control.style.setProperty("--navigator-window-width", `${width}px`);
    }

    function updateMonthlyAxisLabels(retries = 4) {
        const container = document.getElementById("behavior-main-chart-container");
        const layer = document.getElementById("monthly-chart-axis-labels");
        const grid = container && container.querySelector(".apexcharts-grid");
        const startInput = document.getElementById("chart-range-start");
        const endInput = document.getElementById("chart-range-end");
        if (!container || !layer || !startInput?.value || !endInput?.value) return;
        if (!grid || !grid.getBoundingClientRect().width) {
            if (retries > 0) setTimeout(() => updateMonthlyAxisLabels(retries - 1), 60);
            return;
        }

        const firstMonthIndex = Number(container.dataset.firstMonthIndex);
        const monthCount = Number(container.dataset.monthCount);
        const startDate = new Date(`${startInput.value}T00:00:00Z`);
        const endDate = new Date(`${endInput.value}T00:00:00Z`);
        const firstVisible = startDate.getUTCFullYear() * 12 + startDate.getUTCMonth() - firstMonthIndex;
        const lastVisible = endDate.getUTCFullYear() * 12 + endDate.getUTCMonth() - firstMonthIndex;
        const axisMin = firstVisible - 0.5;
        const axisSpan = Math.max(1, lastVisible - firstVisible + 1);
        const gridBounds = grid.getBoundingClientRect();
        const containerBounds = container.getBoundingClientRect();
        const fallbackStep = gridBounds.width / axisSpan;
        let labelOrigin = gridBounds.left - containerBounds.left + (0.5 - firstVisible) * fallbackStep;
        let labelStep = fallbackStep;

        // Apex offsets numeric column bars within the plot area. The grid's
        // left edge is therefore not a reliable origin for their labels. Fit
        // the month positions to one rendered bar series (whose `j` attribute
        // is the zero-based data-point index), then extrapolate across months
        // with zero values as well. Using one series avoids grouped bars
        // shifting a label toward whichever series happens to be nonzero.
        const barSeries = [...container.querySelectorAll(
            ".apexcharts-bar-series .apexcharts-series")];
        const barPoints = barSeries.map(series => [...series.querySelectorAll(
            ".apexcharts-bar-area[j]")]
            .map(bar => {
                const index = Number(bar.getAttribute("j"));
                const bounds = bar.getBoundingClientRect();
                return { index, x: bounds.left + bounds.width / 2 - containerBounds.left,
                    visible: Number.isInteger(index) && bounds.width > 0 && bounds.height > 1 };
            })
            .filter(point => point.visible));
        // A grouped bar's center is NOT the month's center: each behavior
        // occupies a slot on either side of the actual numeric X coordinate.
        // Use the unmodified SCIP coordinates when present. Moving SVG groups
        // breaks Apex's hit testing and gets undone on its next redraw.
        const scipPoints = [...container.querySelectorAll(
            '.apexcharts-line-series .apexcharts-series[seriesName^="SCIP"]')]
            .map(series => [...series.querySelectorAll('.apexcharts-marker[j]')].map(marker => {
                const bounds = marker.getBoundingClientRect();
                return { index: Number(marker.getAttribute('j')),
                    x: bounds.left + bounds.width / 2 - containerBounds.left };
            }));
        const anchors = scipPoints.sort((a, b) => b.length - a.length)[0]?.length
            ? scipPoints[0] : barPoints.sort((a, b) => b.length - a.length)[0] || [];
        if (anchors.length >= 2) {
            const meanIndex = anchors.reduce((sum, point) => sum + point.index, 0) / anchors.length;
            const meanX = anchors.reduce((sum, point) => sum + point.x, 0) / anchors.length;
            const denominator = anchors.reduce((sum, point) =>
                sum + (point.index - meanIndex) ** 2, 0);
            if (denominator > 0) {
                labelStep = anchors.reduce((sum, point) =>
                    sum + (point.index - meanIndex) * (point.x - meanX), 0) / denominator;
                labelOrigin = meanX - meanIndex * labelStep;
            }
        } else if (anchors.length === 1) {
            labelOrigin = anchors[0].x - anchors[0].index * labelStep;
        }

        layer.replaceChildren();
        let previousLabelRight = -Infinity;
        for (let index = Math.max(0, firstVisible);
             index <= Math.min(monthCount - 1, lastVisible); index++) {
            const monthDate = new Date(Date.UTC(0, 0, 1));
            monthDate.setUTCFullYear(Math.floor((firstMonthIndex + index) / 12));
            monthDate.setUTCMonth((firstMonthIndex + index) % 12);
            const label = document.createElement("span");
            label.className = "monthly-chart-axis-label";
            label.textContent = monthDate.toLocaleDateString("en-US", {
                month: "short", year: "numeric", timeZone: "UTC"
            });
            label.style.left = `${labelOrigin + index * labelStep}px`;
            label.style.top = `${gridBounds.bottom - containerBounds.top + 8}px`;
            layer.appendChild(label);
            // These HTML labels bypass ApexCharts' collision handling. Measure
            // their actual width so dense ranges stay readable at any zoom.
            const bounds = label.getBoundingClientRect();
            if (bounds.left < previousLabelRight + 12 ||
                bounds.left < containerBounds.left || bounds.right > containerBounds.right) {
                label.remove();
            } else {
                previousLabelRight = bounds.right;
            }
        }
    }

    async function drainLiveZoomQueue() {
        if (liveZoomInFlight || !pendingLiveZoom) return;

        liveZoomInFlight = true;
        try {
            while (pendingLiveZoom) {
                // Consume only the newest requested position. Pointer input can
                // arrive faster than Apex can redraw a mixed chart, and firing
                // overlapping zoomX promises lets stale frames finish later and
                // makes the chart appear to jump backwards during a drag.
                const zoom = pendingLiveZoom;
                pendingLiveZoom = null;
                try {
                    await window.ApexCharts.exec(
                        "behavior-main-chart",
                        "zoomX",
                        zoom.start,
                        zoom.end);
                    suppressChartZoomEventsUntil = performance.now() + 1000;
                    updateMonthlyAxisLabels();
                } catch (error) {
                    // The component can be replaced while its last drag frame
                    // is queued. Discard that obsolete frame quietly.
                    if (!document.getElementById("navigator-window-control"))
                        pendingLiveZoom = null;
                }
            }
        } finally {
            liveZoomInFlight = false;
            suppressChartZoomEventsUntil = performance.now() + 1000;
            // Cover a request that arrived between the final loop check and
            // clearing the in-flight flag.
            if (pendingLiveZoom) void drainLiveZoomQueue();
        }
    }

    function hideScipPoint() {
        document.getElementById('scip-point-tooltip')?.remove();
        document.getElementById('behavior-main-chart-container')?.classList.remove('scip-point-hover');
    }

    function showScipPoint(point, series, data) {
        hideScipPoint();
        if (data?.y == null) return;
        const container = document.getElementById('behavior-main-chart-container');
        const tooltip = document.createElement('div');
        tooltip.id = 'scip-point-tooltip';
        tooltip.className = 'medication-change-tooltip';
        tooltip.setAttribute('role', 'tooltip');
        const title = document.createElement('strong');
        let date = null;
        if (container.classList.contains('monthly-chart-container')) {
            const month = Number(container.dataset.firstMonthIndex) + Number(data.x);
            date = new Date(Date.UTC(Math.floor(month / 12), month % 12, 1));
            title.textContent = date.toLocaleDateString('en-US', {month:'short', year:'numeric', timeZone:'UTC'});
        } else {
            date = new Date(data.x);
            title.textContent = Number.isNaN(date.getTime()) ? String(data.x) : date.toLocaleDateString('en-US', {timeZone:'UTC'});
        }
        tooltip.append(title);
        const chart = window.Apex?._chartInstances?.find(item => item.id === 'behavior-main-chart')?.chart;
        for (const candidate of chart?.w.config.series || [series]) {
            if (!candidate.name.startsWith('SCIP: ')) continue;
            const match = candidate.data.find(item => String(item.x) === String(data.x));
            if (match?.y == null) continue;
            const value = document.createElement('div');
            value.textContent = `${candidate.name.slice(6)}: ${match.y}`;
            tooltip.appendChild(value);
        }
        document.body.appendChild(tooltip);
        container.classList.add('scip-point-hover');
        const anchor = point.getBoundingClientRect();
        const bounds = tooltip.getBoundingClientRect();
        tooltip.style.left = `${Math.max(8, Math.min(anchor.left, innerWidth - bounds.width - 8))}px`;
        tooltip.style.top = `${Math.max(8, anchor.top - bounds.height - 10)}px`;
    }

    function hideMedicationChange() {
        document.getElementById("medication-change-tooltip")?.remove();
    }

    function showMedicationChange(element, change) {
        hideMedicationChange();
        const tooltip = document.createElement("div");
        tooltip.id = "medication-change-tooltip";
        tooltip.className = "medication-change-tooltip";
        tooltip.setAttribute("role", "tooltip");
        const heading = document.createElement("strong");
        heading.textContent = change.name;
        const date = document.createElement("div");
        date.textContent = change.date;
        const dose = document.createElement("div");
        dose.textContent = `${change.previousDose} → ${change.currentDose} (${change.currentDose > change.previousDose ? "increase" : "decrease"})`;
        tooltip.append(heading, date, dose);
        document.body.appendChild(tooltip);
        const anchor = element.getBoundingClientRect();
        const bounds = tooltip.getBoundingClientRect();
        tooltip.style.left = `${Math.max(8, Math.min(anchor.left, window.innerWidth - bounds.width - 8))}px`;
        const above = anchor.top - bounds.height - 8;
        tooltip.style.top = `${Math.max(8, above >= 8 ? above : Math.min(anchor.bottom + 8, window.innerHeight - bounds.height - 8))}px`;
    }

    function initialize(dotNetReference) {
        if (dotNetReference) dashboardReference = dotNetReference;
        hideMedicationChange();
        hideScipPoint();
        updateMonthlyAxisLabels();
        updateMonthlyNavigatorWindow();
        if (!monthlyAxisResizeInstalled) {
            monthlyAxisResizeInstalled = true;
            let monthlyResizeTimer;
            window.addEventListener("resize", () => {
                hideMedicationChange();
                clearTimeout(monthlyResizeTimer);
                // Apex debounces its resize redraw; align after the new SVG exists.
                monthlyResizeTimer = setTimeout(() => updateMonthlyAxisLabels(), 300);
            });
            window.addEventListener("scroll", () => { hideMedicationChange(); hideScipPoint(); }, true);
        }

        if (!resetHandlerInstalled) {
            resetHandlerInstalled = true;
            // Delegation survives Apex replacing annotations during zoom and redraw.
            const medicationArrow = target => target instanceof Element
                ? target.closest('[class*="medication-change-hit-"]') : null;
            document.addEventListener("mouseover", event => {
                const point = event.target.closest?.('#behavior-main-chart-container .apexcharts-bar-area[j], #behavior-main-chart-container .apexcharts-marker[j]');
                if (point && dashboardReference) {
                    const chart = window.Apex?._chartInstances?.find(item => item.id === 'behavior-main-chart')?.chart;
                    const seriesIndex = Number(point.getAttribute('index'));
                    const pointIndex = Number(point.getAttribute('j'));
                    const series = chart?.w.config.series[seriesIndex];
                    if (series?.name.startsWith('SCIP: ')) showScipPoint(point, series, series.data[pointIndex]);
                    if (series && !series.name.startsWith('SCIP: ') && !series.name.endsWith(' (mg)')) {
                        const data = series.data[pointIndex];
                        if (data?.x != null) dashboardReference.invokeMethodAsync('OnBehaviorPointHover', series.name, data.x);
                    }
                }
                const arrow = medicationArrow(event.target);
                if (!arrow || arrow.contains(event.relatedTarget)) return;
                const key = [...arrow.classList].find(name => name.startsWith("medication-change-hit-"));
                const change = JSON.parse(atob(key.slice("medication-change-hit-".length)));
                showMedicationChange(arrow, change);
            });
            document.addEventListener("mouseout", event => {
                if (event.target.closest?.('.apexcharts-marker')) hideScipPoint();
                const arrow = medicationArrow(event.target);
                if (arrow && !arrow.contains(event.relatedTarget)) hideMedicationChange();
            });
            document.addEventListener("click", function (event) {
                hideMedicationChange();
                const details = document.querySelector(".hover-details-panel");
                if (details && !details.contains(event.target) && dashboardReference) {
                    dashboardReference.invokeMethodAsync("DismissHoverDetails");
                }
                const resetButton = event.target.closest(".apexcharts-reset-icon");
                if (!resetButton || !dashboardReference) return;

                // Apex can consider the full mounted series already reset even
                // while zoomX is displaying a narrow viewport. Route Home to
                // the dashboard's authoritative full-range reset instead.
                event.preventDefault();
                event.stopImmediatePropagation();
                suppressChartZoomEventsUntil = performance.now() + 750;
                pendingLiveZoom = null;
                dashboardReference.invokeMethodAsync("ResetChartToFullRange");
            }, true);
        }

        const control = document.getElementById("navigator-window-control");
        if (!control || control.dataset.livePanInitialized === "true") return;

        control.dataset.livePanInitialized = "true";
        let animationFrame = 0;
        let lastMonthlyWindowKey = null;

        control.addEventListener("input", function () {
            // Apex may report zoomX at a data boundary as a reset-zoom event.
            // Keep navigator-driven events distinct from toolbar zoom/reset.
            suppressChartZoomEventsUntil = performance.now() + 750;
            const isMonthly = control.dataset.period === "Month";
            const selectedMonthIndex = Number(control.value);
            const firstMonthIndex = Number(control.dataset.firstMonthIndex || 0);
            const selectedMonth = firstMonthIndex + selectedMonthIndex;
            const startDay = isMonthly
                ? (Date.UTC(Math.floor(selectedMonth / 12), selectedMonth % 12, 1) - epochMilliseconds)
                    / dayMilliseconds
                : Number(control.value);
            const spanDays = Number(control.dataset.spanDays || 0);
            let endDay = startDay + spanDays;
            const fullMinDay = Number(control.dataset.fullMinDay || startDay);
            const fullMaxDay = Number(control.dataset.fullMaxDay || endDay);
            const fullSpan = Math.max(1, fullMaxDay - fullMinDay);

            const selection = document.querySelector(".navigator-selection-window");
            if (selection && !isMonthly) {
                selection.style.left = `${100 * (startDay - fullMinDay) / fullSpan}%`;
                selection.style.width = `${100 * spanDays / fullSpan}%`;
            }

            const startDate = document.getElementById("chart-range-start");
            const endDate = document.getElementById("chart-range-end");
            if (startDate) startDate.value = formatDate(startDay);
            if (endDate) endDate.value = formatDate(endDay);

            if (animationFrame) cancelAnimationFrame(animationFrame);
            animationFrame = requestAnimationFrame(function () {
                animationFrame = 0;
                if (window.ApexCharts && typeof window.ApexCharts.exec === "function") {
                    let start = epochMilliseconds + startDay * dayMilliseconds;
                    let end = epochMilliseconds + (endDay + 1) * dayMilliseconds - 1;
                    if (isMonthly) {
                        const requestedDate = new Date(start);
                        const periodCount = Math.max(1, Number(control.dataset.periodCount || 1));
                        const firstMonth = Date.UTC(
                            requestedDate.getUTCFullYear(),
                            requestedDate.getUTCMonth(),
                            1);
                        const lastMonth = Date.UTC(
                            requestedDate.getUTCFullYear(),
                            requestedDate.getUTCMonth() + periodCount - 1,
                            1);

                        // Keep a fixed number of monthly buckets on screen.
                        // Half-month padding centers the first and last bars
                        // without admitting another aggregate month.
                        start = firstMonth - 15 * dayMilliseconds;
                        end = lastMonth + 15 * dayMilliseconds;
                        endDay = Math.round((Date.UTC(
                            requestedDate.getUTCFullYear(),
                            requestedDate.getUTCMonth() + periodCount,
                            1) - dayMilliseconds - epochMilliseconds) / dayMilliseconds);

                        const endDateInput = document.getElementById("chart-range-end");
                        if (endDateInput) endDateInput.value = formatDate(endDay);
                        updateMonthlyNavigatorWindow();

                        // The range control moves in days, but monthly data only
                        // changes when it crosses into another month. Avoid
                        // asking Apex to redraw the exact same month window for
                        // every intervening day.
                        const monthlyWindowKey = `${firstMonth}:${lastMonth}`;
                        if (monthlyWindowKey === lastMonthlyWindowKey) return;
                        lastMonthlyWindowKey = monthlyWindowKey;

                        // Monthly charts use a discrete category axis. Zoom by
                        // category index so each tick and bar occupy the exact
                        // same slot.
                        const requestedMonthIndex =
                            (requestedDate.getUTCFullYear() * 12) + requestedDate.getUTCMonth();
                        // Numeric month coordinates are zero-based; bounds sit
                        // halfway between adjacent monthly bars.
                        start = Math.max(-0.5, requestedMonthIndex - firstMonthIndex - 0.5);
                        end = start + periodCount;
                    }
                    pendingLiveZoom = { start, end };
                    void drainLiveZoomQueue();
                }
            });
        }, { passive: true });
    }

    function shouldSuppressChartZoomEvent() {
        return liveZoomInFlight || !!pendingLiveZoom ||
            performance.now() < suppressChartZoomEventsUntil;
    }

    window.chartNavigator = { initialize, shouldSuppressChartZoomEvent, showMedicationChange, hideMedicationChange };
})();
