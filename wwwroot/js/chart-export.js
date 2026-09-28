/** Two-page landscape PDF export for the Psychology Visualization Tool. */
window.chartExport = {
    exportToPdf: async function (title, dateRange, summary) {
        try {
            if (!window.html2canvas || !window.jspdf) throw new Error('PDF export libraries are unavailable.');

            const canvas = await this.captureDashboard(summary || {});

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

    captureDashboard: async function (summary) {
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
        const title = tile.querySelector('.chart-title')?.textContent?.trim() || 'Behavior overview';
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
                header.style.cssText = 'flex:0 0 auto;height:108px;box-sizing:border-box;padding:3px 6px 9px;border-bottom:1px solid #e8ecef;overflow:hidden;color:#1f2933;';
                const eyebrow = clonedDocument.createElement('div');
                eyebrow.textContent = 'BEHAVIOR OVERVIEW';
                eyebrow.style.cssText = 'font:700 12px Arial,sans-serif;letter-spacing:1px;color:#75808b;height:18px;white-space:nowrap;';
                const heading = clonedDocument.createElement('div');
                heading.textContent = title;
                heading.style.cssText = 'font:700 25px Arial,sans-serif;height:34px;line-height:30px;white-space:nowrap;';
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
        const contentWidth = pageWidth - margin * 2;
        const behaviors = summary.behaviors || [];
        const medications = summary.medications || [];
        const missingData = summary.missingData || [];
        const text = value => value === null || value === undefined || value === '' ? '—' : String(value);
        const number = value => Number(value || 0).toLocaleString(undefined, { maximumFractionDigits: 2 });

        pdf.setFillColor(41, 54, 70);
        pdf.rect(0, 0, pageWidth, 22, 'F');
        pdf.setTextColor(255, 255, 255);
        pdf.setFont('helvetica', 'bold');
        pdf.setFontSize(17);
        pdf.text(text(summary.resident || title || 'Behavior History'), margin, 10);
        pdf.setFont('helvetica', 'normal');
        pdf.setFontSize(9);
        pdf.text('Dataset summary', margin, 16);
        pdf.text('Generated ' + new Date().toLocaleString(), pageWidth - margin, 10, { align: 'right' });

        let y = 29;
        const overview = [
            ['Selected range', summary.dateRange || dateRange],
            ['Recorded coverage', summary.dataCoverage],
            ['Grouping / measure', text(summary.grouping) + ' / ' + text(summary.measure)],
            ['View / shifts', text(summary.view) + ' / ' + text(summary.shifts)],
            ['Active overlays', summary.overlays || 'None'],
            ['Total episodes', number(summary.totalEpisodes)]
        ];
        const columnWidth = contentWidth / 3;
        overview.forEach((item, index) => {
            const x = margin + (index % 3) * columnWidth;
            const rowY = y + Math.floor(index / 3) * 11;
            pdf.setFont('helvetica', 'bold');
            pdf.setFontSize(8.5);
            pdf.setTextColor(105, 117, 128);
            pdf.text(item[0].toUpperCase(), x, rowY);
            pdf.setFont('helvetica', 'normal');
            pdf.setTextColor(35, 43, 51);
            pdf.text(pdf.splitTextToSize(text(item[1]), columnWidth - 7)[0], x, rowY + 4.5);
        });

        y += 27;
        pdf.setFont('helvetica', 'bold');
        pdf.setFontSize(11);
        pdf.text('Behavior summary', margin, y);
        y += 4;

        const headers = ['Behavior', 'Episodes', 'Mean rate', 'Periods', 'Avg intensity', 'Duration', 'First', 'Last'];
        const widths = [52, 21, 22, 18, 24, 21, 29, 29];
        const widthScale = contentWidth / widths.reduce((sum, width) => sum + width, 0);
        const scaledWidths = widths.map(width => width * widthScale);
        const rowHeight = Math.max(4, Math.min(6, 88 / Math.max(behaviors.length + 1, 1)));
        const tableFontSize = rowHeight < 5 ? 6.2 : 7.3;

        const drawRow = (cells, rowY, header) => {
            let x = margin;
            if (header) {
                pdf.setFillColor(232, 236, 241);
                pdf.rect(margin, rowY, contentWidth, rowHeight, 'F');
                pdf.setFont('helvetica', 'bold');
            } else pdf.setFont('helvetica', 'normal');
            pdf.setFontSize(tableFontSize);
            pdf.setTextColor(35, 43, 51);
            cells.forEach((cell, index) => {
                pdf.text(pdf.splitTextToSize(text(cell), scaledWidths[index] - 2)[0], x + 1, rowY + rowHeight - 1.4);
                x += scaledWidths[index];
            });
            pdf.setDrawColor(218, 223, 229);
            pdf.line(margin, rowY + rowHeight, margin + contentWidth, rowY + rowHeight);
        };

        drawRow(headers, y, true);
        y += rowHeight;
        behaviors.forEach(item => {
            drawRow([
                item.behavior, number(item.totalEpisodes), number(item.meanRate), number(item.recordedPeriods),
                item.averageIntensity == null ? 'Unavailable' : number(item.averageIntensity),
                item.totalDuration > 0 ? number(item.totalDuration) : 'Unavailable',
                item.firstPeriod, item.lastPeriod
            ], y, false);
            y += rowHeight;
        });
        if (!behaviors.length) {
            drawRow(['No behavior data in the selected range', '', '', '', '', '', '', ''], y, false);
            y += rowHeight;
        }

        y += 7;
        const halfWidth = (contentWidth - 8) / 2;
        pdf.setFont('helvetica', 'bold');
        pdf.setFontSize(10);
        pdf.text('Medications', margin, y);
        pdf.text('Missing data', margin + halfWidth + 8, y);
        pdf.setFont('helvetica', 'normal');
        pdf.setFontSize(7.5);
        const lineCount = Math.max(1, Math.floor((pageHeight - y - 15) / 5));

        const medLines = medications.length ? medications : [{ medication: 'None in selected range' }];
        medLines.slice(0, lineCount).forEach((item, index) => {
            const line = item.doseRange ? `${item.medication}: ${item.doseRange}  (${item.start} – ${item.end})` : item.medication;
            pdf.text(pdf.splitTextToSize(text(line), halfWidth)[0], margin, y + 6 + index * 5);
        });
        if (medLines.length > lineCount) pdf.text(`+ ${medLines.length - lineCount} additional medication(s)`, margin, y + 6 + (lineCount - 1) * 5);

        const missingLines = missingData.length ? missingData : [{ reason: 'None in selected range' }];
        missingLines.slice(0, lineCount).forEach((item, index) => {
            const line = item.start ? `${item.reason}: ${item.start} – ${item.end}` : item.reason;
            pdf.text(pdf.splitTextToSize(text(line), halfWidth)[0], margin + halfWidth + 8, y + 6 + index * 5);
        });
        if (missingLines.length > lineCount) pdf.text(`+ ${missingLines.length - lineCount} additional range(s)`, margin + halfWidth + 8, y + 6 + (lineCount - 1) * 5);

        pdf.setTextColor(120, 130, 140);
        pdf.setFontSize(7);
        pdf.text('Rates are mean displayed-period rates; duration is the total recorded duration count for the selected range.', margin, pageHeight - 6);
        pdf.text('Page 2 of 2', pageWidth - margin, pageHeight - 6, { align: 'right' });
    }
};
