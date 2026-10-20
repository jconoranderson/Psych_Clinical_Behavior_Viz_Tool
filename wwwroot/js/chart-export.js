/** Chart and summary landscape PDF export for the Psychology Visualization Tool. */
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
        const chart = document.getElementById('behavior-main-chart-container');
        if (!chart) throw new Error('The main chart is unavailable.');
        const tile = chart.closest('.chart-card');
        const bounds = chart.getBoundingClientRect();
        if (bounds.width <= 0 || bounds.height <= 0) throw new Error('The chart has no visible area.');

        // Build a standalone export at the document origin. Capturing the full
        // app and cropping viewport coordinates can include headers or navigator
        // controls when the capture renderer lays out the page differently.
        const stage = document.createElement('div');
        stage.className = 'pdf-chart-export';
        stage.style.cssText = `position:absolute;left:0;top:0;width:${bounds.width + 32}px;padding:16px;box-sizing:border-box;background:#fff;color:#263238;z-index:-1;font-family:Arial,sans-serif;`;
        const heading = document.createElement('div');
        heading.textContent = exportTitle || tile?.querySelector('.chart-title')?.textContent || 'Behavior overview';
        heading.style.cssText = 'font-size:26px;font-weight:700;line-height:1.25;margin-bottom:16px;overflow-wrap:anywhere;';
        stage.appendChild(heading);

        const legend = document.createElement('div');
        legend.style.cssText = 'display:block;text-align:center;padding:0 0 12px;font-size:14px;line-height:24px;';
        for (const item of tile?.querySelectorAll('.chart-legend-entry') || []) {
            const entry = document.createElement('span');
            // Inline blocks wrap between complete entries, never within names.
            entry.style.cssText = 'display:inline-block;white-space:nowrap;margin:3px 14px;vertical-align:middle;';
            const originalSwatch = item.querySelector('div');
            if (originalSwatch) {
                const swatch = originalSwatch.cloneNode(true);
                swatch.style.display = 'inline-block';
                swatch.style.verticalAlign = 'middle';
                swatch.style.marginRight = '7px';
                entry.appendChild(swatch);
            }
            const label = document.createElement('span');
            label.textContent = [...item.querySelectorAll('.mud-typography')].map(node => node.textContent.trim()).join(' ');
            label.style.cssText = 'white-space:nowrap;font-weight:600;vertical-align:middle;';
            entry.appendChild(label);
            legend.appendChild(entry);
        }
        stage.appendChild(legend);

        const plot = chart.cloneNode(true);
        plot.removeAttribute('id');
        plot.style.cssText = `position:relative;width:${bounds.width}px;height:${bounds.height}px;overflow:hidden;`;
        plot.querySelectorAll('.apexcharts-toolbar, .apexcharts-tooltip, .apexcharts-xaxistooltip, .apexcharts-yaxistooltip, .apexcharts-legend').forEach(node => node.remove());
        stage.appendChild(plot);
        document.body.appendChild(stage);
        try {
            await document.fonts?.ready;
            return await window.html2canvas(stage, {
                backgroundColor: '#ffffff',
                scale: 2,
                useCORS: true,
                allowTaint: false,
                logging: false,
                foreignObjectRendering: false,
                scrollX: 0,
                scrollY: 0,
                windowWidth: Math.max(document.documentElement.clientWidth, Math.ceil(bounds.width + 32)),
                windowHeight: Math.max(document.documentElement.clientHeight, stage.offsetHeight)
            });
        } finally {
            stage.remove();
        }
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
