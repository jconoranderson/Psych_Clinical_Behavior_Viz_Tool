/**
 * Chart export utilities for Psychology Visualization Tool
 * Exports ApexCharts to PDF using jsPDF
 */
window.chartExport = {
    /**
     * Export the first ApexChart on the page to a PDF download
     * @param {string} title - Chart title for the PDF header
     * @param {string} dateRange - Date range text for the PDF header
     */
    exportToPdf: async function (title, dateRange) {
        try {
            // Find the chart instance
            const chartElements = document.querySelectorAll('.apexcharts-canvas');
            if (chartElements.length === 0) {
                console.error('No ApexCharts found on page');
                return;
            }

            // Get the chart's SVG and convert to image
            const chartEl = chartElements[0];
            const svgElement = chartEl.querySelector('svg');
            if (!svgElement) {
                console.error('No SVG found in chart');
                return;
            }

            // Clone SVG and serialize
            const svgClone = svgElement.cloneNode(true);
            const svgData = new XMLSerializer().serializeToString(svgClone);
            const svgBlob = new Blob([svgData], { type: 'image/svg+xml;charset=utf-8' });
            const url = URL.createObjectURL(svgBlob);

            // Create image from SVG
            const img = new Image();
            img.onload = function () {
                // Create canvas to convert SVG to raster
                const canvas = document.createElement('canvas');
                const scale = 2; // High DPI
                canvas.width = img.width * scale;
                canvas.height = img.height * scale;

                const ctx = canvas.getContext('2d');
                ctx.scale(scale, scale);
                ctx.fillStyle = 'white';
                ctx.fillRect(0, 0, img.width, img.height);
                ctx.drawImage(img, 0, 0);

                const imgData = canvas.toDataURL('image/png');

                // Create PDF (landscape)
                const { jsPDF } = window.jspdf;
                const pdf = new jsPDF({
                    orientation: 'landscape',
                    unit: 'mm',
                    format: 'letter'
                });

                const pageWidth = pdf.internal.pageSize.getWidth();
                const pageHeight = pdf.internal.pageSize.getHeight();
                const margin = 15;

                // Header
                pdf.setFontSize(16);
                pdf.setFont(undefined, 'bold');
                pdf.text(title || 'Behavior History', margin, margin + 5);

                pdf.setFontSize(10);
                pdf.setFont(undefined, 'normal');
                pdf.setTextColor(100, 100, 100);
                if (dateRange) {
                    pdf.text(dateRange, margin, margin + 12);
                }
                pdf.text('Generated: ' + new Date().toLocaleDateString(), pageWidth - margin - 50, margin + 5);

                // Chart image
                const chartTop = margin + 18;
                const availWidth = pageWidth - (margin * 2);
                const availHeight = pageHeight - chartTop - margin;
                const aspectRatio = img.width / img.height;

                let chartWidth = availWidth;
                let chartHeight = chartWidth / aspectRatio;

                if (chartHeight > availHeight) {
                    chartHeight = availHeight;
                    chartWidth = chartHeight * aspectRatio;
                }

                const xOffset = margin + (availWidth - chartWidth) / 2;
                pdf.addImage(imgData, 'PNG', xOffset, chartTop, chartWidth, chartHeight);

                // Download
                const filename = (title || 'behavior-history').replace(/[^a-zA-Z0-9]/g, '_').toLowerCase();
                pdf.save(filename + '.pdf');

                URL.revokeObjectURL(url);
            };
            img.src = url;
        } catch (err) {
            console.error('PDF export failed:', err);
        }
    }
};
