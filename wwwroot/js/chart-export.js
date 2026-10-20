/** Two-page landscape PDF export for the Psychology Visualization Tool. */
window.chartExport = {
    requestTitle: function (name) {
        const suffix = window.prompt(`Optional chart title to add after "${name}". Leave blank to use the name only.`, '');
        return suffix === null ? null : (suffix.trim() ? `${name} - ${suffix.trim()}` : name);
    },

    exportToPdf: async function (title, dateRange, summary) {
        try {
            if (!window.html2canvas || !window.jspdf) throw new Error('PDF export libraries are unavailable.');

            const canvas = await this.captureDashboard(summary || {}, title);

            const { jsPDF } = window.jspdf;
            const pdf = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'letter' });
            const pageWidth = pdf.internal.pageSize.getWidth();
            const pageHeight = pdf.internal.pageSize.getHeight();

            // Page 1 contains only the main chart tile as currently displayed.
            const screenshot = canvas.toDataURL('image/jpeg', 0.94);
            const imageRatio = canvas.width / canvas.height;
            const pageRatio = pageWidth / pageHeight;
            let imageWidth, imageHeight, imageX = 0, imageY = 0;
            if (imageRatio > pageRatio) {
                imageWidth = pageWidth;
                imageHeight = pageWidth / imageRatio;
                imageY = (pageHeight - imageHeight) / 2;
            } else {
                imageHeight = pageHeight;
                imageWidth = pageHeight * imageRatio;
                imageX = (pageWidth - imageWidth) / 2;
            }
            pdf.addImage(screenshot, 'JPEG', imageX, imageY, imageWidth, imageHeight, undefined, 'FAST');

            pdf.addPage('letter', 'landscape');
            this.drawSummaryPage(pdf, summary || {}, title, dateRange, pageWidth, pageHeight);

            const filename = (title || 'behavior-history').replace(/[^a-zA-Z0-9]+/g, '_')
                .replace(/^_+|_+$/g, '').toLowerCase();
            pdf.save((filename || 'behavior-history') + '.pdf');
        } catch (error) {
            console.error('PDF export failed:', error);
            const detail = error && error.message ? `\n\n${error.message}` : '';
            window.alert('The PDF could not be created.' + detail);
        }
    },

    captureDashboard: async function (summary, exportTitle) {
        const tile = document.querySelector('.chart-card');
        if (!tile) throw new Error('The main chart tile is unavailable.');
        const root = document.querySelector('.mud-layout') || document.body;
        const bounds = tile.getBoundingClientRect();
        const viewportWidth = document.documentElement.clientWidth;
        const viewportHeight = document.documentElement.clientHeight;
        const dateValues = ['chart-range-start', 'chart-range-end'].map(id => {
            const input = document.getElementById(id);
            return { value: input?.value || '' };
        });
        const title = exportTitle || tile.querySelector('.chart-title')?.textContent?.trim() || 'Behavior overview';
        const legendItems = [...tile.querySelectorAll('.apexcharts-legend-series')]
            .map(series => ({
                text: series.querySelector('.apexcharts-legend-text')?.textContent?.trim() || '',
                color: getComputedStyle(series.querySelector('.apexcharts-legend-marker') || series).backgroundColor
            }))
            .filter(item => item.text)
            .filter((item, index, items) => items.findIndex(other => other.text === item.text) === index);
        const captureOptions = {
            backgroundColor: '#ffffff',
            scale: 2,
            useCORS: true,
            allowTaint: false,
            logging: false,
            width: viewportWidth,
            height: viewportHeight,
            windowWidth: viewportWidth,
            windowHeight: viewportHeight,
            scrollX: 0,
            scrollY: -window.scrollY,
            onclone: clonedDocument => {
                const style = clonedDocument.createElement('style');
                style.textContent = `
                    .chart-card { position: relative !important; }
                    .chart-card .apexcharts-legend { visibility: hidden !important; }
                `;
                clonedDocument.head.appendChild(style);

                const card = clonedDocument.querySelector('.chart-card');
                if (!card) return;
                // html2canvas reflows MudBlazor's interactive controls into
                // overlapping rows. Replace only the cloned controls with a
                // compact, non-interactive record of the same settings.
                card.querySelector('.chart-heading-row')?.remove();
                card.querySelector('.dashboard-toolbar')?.remove();
                const header = clonedDocument.createElement('div');
                header.style.cssText = 'flex:0 0 auto;min-height:108px;box-sizing:border-box;padding:3px 6px 9px;border-bottom:1px solid #e8ecef;overflow:hidden;color:#1f2933;';
                const eyebrow = clonedDocument.createElement('div');
                eyebrow.textContent = 'BEHAVIOR OVERVIEW';
                eyebrow.style.cssText = 'font:700 12px Arial,sans-serif;letter-spacing:1px;color:#75808b;height:18px;white-space:nowrap;';
                const heading = clonedDocument.createElement('div');
                heading.textContent = title;
                heading.style.cssText = 'font:700 25px Arial,sans-serif;min-height:34px;line-height:30px;overflow-wrap:anywhere;';
                const settings = clonedDocument.createElement('div');
                const dateRange = dateValues.map(({ value }) => {
                    const parts = value.split('-');
                    return parts.length === 3 ? `${parts[1]}/${parts[2]}/${parts[0]}` : '—';
                }).join(' – ');
                settings.textContent = [
                    `${summary.view || 'Chart'} view`,
                    `${summary.measure || 'Rate'} measure`,
                    `${summary.grouping || 'Month'} grouping`,
                    `Shifts: ${summary.shifts || 'All'}`
                ].join('   •   ');
                settings.style.cssText = 'font:600 13px Arial,sans-serif;color:#52606d;height:22px;line-height:22px;white-space:nowrap;';
                const settingsDetails = clonedDocument.createElement('div');
                settingsDetails.textContent = [
                    `Overlays: ${summary.overlays || 'None'}`,
                    dateRange
                ].join('   •   ');
                settingsDetails.style.cssText = 'font:600 13px Arial,sans-serif;color:#52606d;height:22px;line-height:22px;white-space:nowrap;';
                header.append(eyebrow, heading, settings, settingsDetails);
                card.prepend(header);

                card.querySelectorAll('#navigator-window-control, .navigator-window-input').forEach(input => input.remove());

                if (legendItems.length) {
                    const legend = clonedDocument.createElement('div');
                    legend.style.cssText = 'position:absolute;left:10px;right:10px;bottom:5px;min-height:27px;background:white;display:flex;align-items:center;justify-content:center;gap:16px;flex-wrap:wrap;z-index:5;font:600 13px Arial,sans-serif;color:#4b5563;';
                    for (const item of legendItems) {
                        const entry = clonedDocument.createElement('span');
                        entry.style.cssText = 'display:inline-flex;align-items:center;gap:5px;white-space:nowrap;';
                        const marker = clonedDocument.createElement('span');
                        marker.style.cssText = `display:inline-block;width:11px;height:11px;background:${item.color};border-radius:2px;`;
                        entry.append(marker, clonedDocument.createTextNode(item.text));
                        legend.appendChild(entry);
                    }
                    card.appendChild(legend);
                }
            }
        };
        let pageCanvas;
        try {
            // Browser-native foreign-object rendering avoids failures when the
            // component library uses newer CSS color functions.
            pageCanvas = await window.html2canvas(root, {
                ...captureOptions,
                foreignObjectRendering: true
            });
        } catch (captureError) {
            console.warn('Page capture failed; retrying with standard rendering.', captureError);
            pageCanvas = await window.html2canvas(root, {
                ...captureOptions,
                foreignObjectRendering: false
            });
        }

        // Cropping the known-good page capture avoids html2canvas applying the
        // tile's viewport offset twice when foreign-object rendering an element.
        const scaleX = pageCanvas.width / viewportWidth;
        const scaleY = pageCanvas.height / viewportHeight;
        const sourceX = Math.max(0, Math.round(bounds.left * scaleX));
        const sourceY = Math.max(0, Math.round(bounds.top * scaleY));
        const sourceWidth = Math.min(pageCanvas.width - sourceX,
            Math.round(bounds.width * scaleX));
        const sourceHeight = Math.min(pageCanvas.height - sourceY,
            Math.round(bounds.height * scaleY));
        if (sourceWidth <= 0 || sourceHeight <= 0)
            throw new Error('The main chart tile is outside the captured page.');

        const tileCanvas = document.createElement('canvas');
        tileCanvas.width = sourceWidth;
        tileCanvas.height = sourceHeight;
        tileCanvas.getContext('2d').drawImage(pageCanvas,
            sourceX, sourceY, sourceWidth, sourceHeight,
            0, 0, sourceWidth, sourceHeight);
        return tileCanvas;
    },

    drawSummaryPage: function (pdf, summary, title, dateRange, pageWidth, pageHeight) {
        const margin = 12;
        const width = pageWidth - 2 * margin;
        const text = value => value == null || value === '' ? '—' : String(value);
        const number = value => Number(value || 0).toLocaleString(undefined, { maximumFractionDigits: 2 });
        let y;
        const header = () => {
            pdf.setFont('helvetica', 'bold');
            pdf.setFontSize(18);
            const lines = pdf.splitTextToSize(text(title || summary.resident || 'Behavior History'), width);
            const height = 17 + lines.length * 7;
            pdf.setFillColor(41, 54, 70);
            pdf.rect(0, 0, pageWidth, height, 'F');
            pdf.setTextColor(255, 255, 255);
            pdf.text(lines, margin, 10);
            pdf.setFont('helvetica', 'normal');
            pdf.setFontSize(11);
            pdf.text('Selected timeframe summary', margin, height - 4);
            pdf.setTextColor(35, 43, 51);
            y = height + 9;
        };
        const ensureSpace = height => {
            if (y + height > pageHeight - 14) {
                pdf.addPage('letter', 'landscape');
                header();
            }
        };
        const section = label => {
            ensureSpace(18);
            pdf.setFont('helvetica', 'bold');
            pdf.setFontSize(14);
            pdf.text(label, margin, y);
            y += 6;
        };
        header();
        const overview = [
            ['Selected range', summary.dateRange || dateRange],
            ['View / shifts', `${text(summary.view)} / ${text(summary.shifts)}`],
            ['Total episodes', number(summary.totalEpisodes)]
        ];
        overview.forEach(([label, value]) => {
            pdf.setFont('helvetica', 'bold');
            pdf.setFontSize(11);
            pdf.text(label, margin, y);
            pdf.setFont('helvetica', 'normal');
            pdf.text(text(value), margin + 40, y);
            y += 7;
        });
        y += 4;
        section('Behavior summary');
        const widths = [width * .46, width * .18, width * .18, width * .18];
        const drawRow = (cells, isHeader = false) => {
            pdf.setFont('helvetica', isHeader ? 'bold' : 'normal');
            pdf.setFontSize(11);
            const lines = cells.map((value, index) => pdf.splitTextToSize(text(value), widths[index] - 4));
            const height = Math.max(...lines.map(value => value.length)) * 5 + 4;
            ensureSpace(height);
            pdf.setFont('helvetica', isHeader ? 'bold' : 'normal');
            pdf.setFontSize(11);
            if (isHeader) {
                pdf.setFillColor(232, 236, 241);
                pdf.rect(margin, y, width, height, 'F');
            }
            let x = margin;
            lines.forEach((value, index) => {
                pdf.text(value, x + 2, y + 5);
                x += widths[index];
            });
            y += height;
            pdf.setDrawColor(218, 223, 229);
            pdf.line(margin, y, margin + width, y);
        };
        drawRow(['Behavior', 'Episodes', 'Mean rate', 'Periods'], true);
        for (const item of summary.behaviors || []) {
            drawRow([item.behavior, number(item.totalEpisodes), number(item.meanRate), number(item.recordedPeriods)]);
        }
        if (!summary.behaviors?.length) drawRow(['No behavior data in the selected range', '', '', '']);
        y += 10;
        section('Medication Change');
        const medications = summary.medications?.length ? summary.medications : [{ medication: 'None in selected range' }];
        for (const item of medications) {
            pdf.setFont('helvetica', 'normal');
            pdf.setFontSize(11);
            const line = item.doseRange ? `${item.medication}: ${item.doseRange}  (${item.start} – ${item.end})` : item.medication;
            const lines = pdf.splitTextToSize(line, width);
            ensureSpace(lines.length * 5 + 3);
            pdf.setFont('helvetica', 'normal');
            pdf.setFontSize(11);
            pdf.text(lines, margin, y + 4);
            y += lines.length * 5 + 3;
        }
        ensureSpace(12);
        y += 7;
        pdf.setTextColor(100, 110, 120);
        pdf.setFontSize(10);
        pdf.text('Mean rate is the average of the displayed-period rates within the selected range.', margin, y);
    }
};
