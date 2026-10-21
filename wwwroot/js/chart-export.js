/** Chart and summary landscape PDF export for the Psychology Visualization Tool. */
window.chartExport = {
    requestOptions: function (name) {
        return new Promise(resolve => {
            const dialog = document.createElement('dialog');
            dialog.className = 'pdf-export-dialog';
            dialog.setAttribute('aria-labelledby', 'pdf-export-heading');
            const form = document.createElement('form');
            form.method = 'dialog';
            const heading = document.createElement('h2');
            heading.id = 'pdf-export-heading';
            heading.textContent = 'Export chart to PDF';
            const titleLabel = document.createElement('label');
            titleLabel.htmlFor = 'pdf-title-suffix';
            titleLabel.textContent = 'Title addition (optional)';
            const title = document.createElement('input');
            title.id = 'pdf-title-suffix';
            title.type = 'text';
            title.placeholder = 'For example: Horse riding';
            const preview = document.createElement('p');
            preview.className = 'pdf-title-preview';
            preview.textContent = name;
            title.addEventListener('input', () => {
                preview.textContent = title.value.trim() ? `${name} - ${title.value.trim()}` : name;
            });
            const narrativeLabel = document.createElement('label');
            narrativeLabel.htmlFor = 'pdf-narrative';
            narrativeLabel.textContent = 'Narrative description (optional)';
            const narrative = document.createElement('textarea');
            narrative.id = 'pdf-narrative';
            narrative.rows = 6;
            narrative.placeholder = 'Describe what was happening during this timeframe. This text will appear below the chart.';
            const actions = document.createElement('div');
            actions.className = 'pdf-export-actions';
            const cancel = document.createElement('button');
            cancel.type = 'button';
            cancel.textContent = 'Cancel';
            cancel.addEventListener('click', () => dialog.close());
            const submit = document.createElement('button');
            submit.type = 'submit';
            submit.textContent = 'Export PDF';
            let result = null;
            form.addEventListener('submit', event => {
                event.preventDefault();
                result = { title: preview.textContent, narrative: narrative.value.trim() };
                dialog.close();
            });
            dialog.addEventListener('close', () => { dialog.remove(); resolve(result); }, { once: true });
            actions.append(cancel, submit);
            form.append(heading, titleLabel, title, preview, narrativeLabel, narrative, actions);
            dialog.appendChild(form);
            document.body.appendChild(dialog);
            dialog.showModal();
            title.focus();
        });
    },

    exportToPdf: async function (title, dateRange, summary, narrative = '') {
        try {
            if (!window.html2canvas || !window.jspdf) throw new Error('PDF export libraries are unavailable.');

            const canvas = await this.captureDashboard(summary || {}, title);

            const { jsPDF } = window.jspdf;
            const pdf = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'letter' });
            const pageWidth = pdf.internal.pageSize.getWidth();
            const pageHeight = pdf.internal.pageSize.getHeight();

            this.drawChartPage(pdf, canvas, narrative, pageWidth, pageHeight);

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

    drawChartPage: function (pdf, canvas, narrative, pageWidth, pageHeight) {
        const margin = 12;
        const width = pageWidth - margin * 2;
        const lineHeight = 5;
        pdf.setFont('helvetica', 'normal');
        pdf.setFontSize(11);
        const lines = narrative.trim() ? pdf.splitTextToSize(narrative.trim(), width) : [];
        const firstLineCount = Math.min(lines.length, 12);
        const narrativeHeight = firstLineCount ? firstLineCount * lineHeight + 7 : 0;
        const availableHeight = pageHeight - margin * 2 - narrativeHeight;
        const ratio = canvas.width / canvas.height;
        const imageWidth = Math.min(width, availableHeight * ratio);
        const imageHeight = imageWidth / ratio;
        const firstPageLines = Math.min(lines.length,
            Math.max(0, Math.floor((pageHeight - margin * 2 - imageHeight - 7) / lineHeight)));
        const blockHeight = imageHeight + (firstPageLines ? 7 + firstPageLines * lineHeight : 0);
        const top = Math.max(margin, (pageHeight - blockHeight) / 2);
        pdf.addImage(canvas.toDataURL('image/jpeg', 0.94), 'JPEG',
            (pageWidth - imageWidth) / 2, top, imageWidth, imageHeight, undefined, 'FAST');
        let offset = 0;
        let y = top + imageHeight + 7;
        let count = firstPageLines;
        while (offset < lines.length) {
            const chunk = lines.slice(offset, offset + count);
            chunk.forEach((line, index) => pdf.text(line, margin, y + index * lineHeight));
            offset += chunk.length;
            if (offset < lines.length) {
                pdf.addPage('letter', 'landscape');
                count = Math.min(lines.length - offset, Math.floor((pageHeight - margin * 2) / lineHeight));
                y = (pageHeight - count * lineHeight) / 2 + lineHeight;
            }
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
        const behaviorLegend = document.createElement('div');
        const medicationLegend = document.createElement('div');
        const scipLegend = document.createElement('div');
        legend.append(behaviorLegend, medicationLegend, scipLegend);
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
                // Preserve horizontal gradient swatches without relying on flex.
                for (const step of swatch.children) step.style.display = 'inline-block';
                entry.appendChild(swatch);
            }
            const label = document.createElement('span');
            label.textContent = [...item.querySelectorAll('.mud-typography')].map(node => node.textContent.trim()).join(' ');
            label.style.cssText = 'white-space:nowrap;font-weight:600;vertical-align:middle;';
            entry.appendChild(label);
            (item.dataset.legendKind === 'medication' ? medicationLegend
                : item.dataset.legendKind === 'scip' ? scipLegend : behaviorLegend).appendChild(entry);
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
