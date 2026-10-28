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

            this.drawChartPage(pdf, canvas, narrative, pageWidth, pageHeight, title);

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

    drawChartPage: function (pdf, canvas, narrative, pageWidth, pageHeight, title) {
        const margin = 6;
        const width = pageWidth - margin * 2;
        const headerHeight = this.drawTitleBlock(pdf, title, 'Behavior chart', pageWidth);
        const contentTop = headerHeight + margin;
        const contentHeight = pageHeight - contentTop - margin;
        const narrativeMargin = 16;
        const narrativeWidth = pageWidth - narrativeMargin * 2;
        const lineHeight = 5;
        pdf.setFont('helvetica', 'normal');
        pdf.setFontSize(11);
        const lines = narrative.trim() ? pdf.splitTextToSize(narrative.trim(), narrativeWidth) : [];
        const firstLineCount = Math.min(lines.length, 12);
        const narrativeHeight = firstLineCount ? firstLineCount * lineHeight + 7 : 0;
        const availableHeight = contentHeight - narrativeHeight;
        // One uniform scale preserves the chart, text and marker proportions.
        // Fit to both page dimensions; never stretch or crop the chart.
        const scale = Math.min(width / canvas.width, availableHeight / canvas.height);
        const imageWidth = canvas.width * scale;
        const imageHeight = canvas.height * scale;
        const firstPageLines = Math.min(lines.length,
            Math.max(0, Math.floor((contentHeight - imageHeight - 7) / lineHeight)));
        // Center the chart and its narrative as one block in the white area.
        const blockHeight = imageHeight + (firstPageLines ? 7 + firstPageLines * lineHeight : 0);
        const top = contentTop + Math.max(0, (contentHeight - blockHeight) / 2);
        pdf.addImage(canvas.toDataURL('image/jpeg', 0.94), 'JPEG',
            (pageWidth - imageWidth) / 2, top, imageWidth, imageHeight, undefined, 'FAST');
        let offset = 0;
        let y = top + imageHeight + 7;
        let count = firstPageLines;
        while (offset < lines.length) {
            const chunk = lines.slice(offset, offset + count);
            chunk.forEach((line, index) => pdf.text(line, narrativeMargin, y + index * lineHeight));
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
        stage.style.cssText = `position:absolute;left:0;top:0;width:${bounds.width + 8}px;padding:4px;box-sizing:border-box;background:#fff;color:#263238;z-index:-1;font-family:Arial,sans-serif;`;
        const chartTitle = document.createElement('h2');
        chartTitle.textContent = exportTitle || summary.resident || 'Behavior History';
        chartTitle.style.cssText = 'margin:0;padding:8px 16px 16px;text-align:center;font-size:24px;line-height:1.3;font-weight:600;overflow-wrap:anywhere;';
        stage.appendChild(chartTitle);
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

    drawTitleBlock: function (pdf, title, subtitle, pageWidth) {
        pdf.setFont('helvetica', 'bold');
        pdf.setFontSize(18);
        const lines = pdf.splitTextToSize(title || 'Behavior History', pageWidth - 24);
        const height = 17 + lines.length * 7;
        pdf.setFillColor(41, 54, 70);
        pdf.rect(0, 0, pageWidth, height, 'F');
        pdf.setTextColor(255, 255, 255);
        pdf.text(lines, 12, 10);
        pdf.setFont('helvetica', 'normal');
        pdf.setFontSize(11);
        pdf.text(subtitle, 12, height - 4);
        pdf.setTextColor(35, 43, 51);
        return height;
    },

    drawSummaryPage: function (pdf, summary, title, dateRange, pageWidth, pageHeight) {
        const margin = 12;
        const width = pageWidth - 2 * margin;
        const text = value => value == null || value === '' ? '—' : String(value);
        const number = value => Number(value || 0).toLocaleString(undefined, { maximumFractionDigits: 2 });
        let y;
        const header = () => {
            const height = this.drawTitleBlock(pdf, title || summary.resident,
                'Selected timeframe summary', pageWidth);
            y = height + 6;
        };
        const ensureSpace = height => {
            if (y + height > pageHeight - 14) {
                pdf.addPage('letter', 'landscape');
                header();
            }
        };
        const section = label => {
            ensureSpace(12);
            pdf.setFont('helvetica', 'bold');
            pdf.setFontSize(12);
            pdf.text(label, margin, y);
            y += 5;
        };
        header();
        const overview = [
            ['Selected range', summary.dateRange || dateRange],
            ['View / shifts', `${text(summary.view)} / ${text(summary.shifts)}`],
            ['Days of week', summary.days === '' ? 'None selected' : summary.days || 'All days'],
            ['Total episodes', number(summary.totalEpisodes)]
        ];
        overview.forEach(([label, value]) => {
            pdf.setFont('helvetica', 'bold');
            pdf.setFontSize(11);
            pdf.text(label, margin, y);
            pdf.setFont('helvetica', 'normal');
            pdf.text(text(value), margin + 40, y);
            y += 5.5;
        });
        y += 2;
        section('Behavior summary');
        const widths = [width * .46, width * .18, width * .18, width * .18];
        const drawRow = (cells, isHeader = false, columnWidths = widths) => {
            pdf.setFont('helvetica', isHeader ? 'bold' : 'normal');
            pdf.setFontSize(11);
            const lines = cells.map((value, index) => pdf.splitTextToSize(text(value), columnWidths[index] - 4));
            const height = Math.max(...lines.map(value => value.length)) * 4.5 + 2;
            ensureSpace(height);
            pdf.setFont('helvetica', isHeader ? 'bold' : 'normal');
            pdf.setFontSize(11);
            if (isHeader) {
                pdf.setFillColor(232, 236, 241);
                pdf.rect(margin, y, width, height, 'F');
            }
            let x = margin;
            lines.forEach((value, index) => {
                pdf.text(value, x + 2, y + 4.5);
                x += columnWidths[index];
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
        y += 7;
        if (summary.scip?.length) {
            section('SCIP summary');
            // Pair maneuvers across the landscape page to keep the summary
            // compact without shrinking the readable body text.
            const scipWidths = [width * .38, width * .12, width * .38, width * .12];
            drawRow(['Maneuver', 'Count', 'Maneuver', 'Count'], true, scipWidths);
            for (let index = 0; index < summary.scip.length; index += 2) {
                const left = summary.scip[index];
                const right = summary.scip[index + 1];
                drawRow([left.maneuver, number(left.total), right?.maneuver || '',
                    right ? number(right.total) : ''], false, scipWidths);
            }
            y += 7;
        }
        section('Medication Change');
        const medications = summary.medications?.length ? summary.medications : [{ medication: 'No dose changes in selected range' }];
        const columnGap = 10;
        const columnWidth = (width - columnGap) / 2;
        let column = 0;
        let columnTop = y;
        for (const item of medications) {
            pdf.setFont('helvetica', 'normal');
            pdf.setFontSize(11);
            const delta = item.delta == null ? '' : `${item.delta > 0 ? '+' : ''}${number(item.delta)} ${item.unit || ''}`.trim();
            const line = item.date
                ? `${item.date} — ${item.medication}: ${delta}`
                : item.medication;
            const lines = pdf.splitTextToSize(line, columnWidth);
            // Continue down the right column before allocating another page.
            // Split very long entries too, rather than drawing outside a page.
            for (let offset = 0; offset < lines.length;) {
                if (y + 6 > pageHeight - 12) {
                    if (column === 0) {
                        column = 1;
                        y = columnTop;
                    } else {
                        pdf.addPage('letter', 'landscape');
                        header();
                        section('Medication Change (continued)');
                        column = 0;
                        columnTop = y;
                    }
                }
                const count = Math.max(1, Math.floor((pageHeight - 12 - y - 1.5) / 4.5));
                const chunk = lines.slice(offset, offset + count);
                pdf.setFont('helvetica', 'normal');
                pdf.setFontSize(11);
                pdf.text(chunk, margin + column * (columnWidth + columnGap), y + 4);
                y += chunk.length * 4.5;
                offset += chunk.length;
            }
            y += 1.5;
        }

    }
};
