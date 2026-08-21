(function (global) {
    'use strict';

    function cssVar(name) {
        return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    }

    function readJsonData(scriptId) {
        var el = document.getElementById(scriptId);
        if (!el) {
            return null;
        }
        try {
            return JSON.parse(el.textContent);
        } catch (e) {
            console.error('CopilotCharts: failed to parse chart data for #' + scriptId, e);
            return null;
        }
    }

    // Vertical hairline that follows the hovered/focused point — see
    // dataviz skill references/interaction.md ("the crosshair finds the X").
    var crosshairPlugin = {
        id: 'copilotCrosshair',
        afterDatasetsDraw: function (chart) {
            var active = chart.getActiveElements ? chart.getActiveElements() : [];
            if (!active.length) {
                return;
            }
            var ctx = chart.ctx;
            var area = chart.chartArea;
            var x = active[0].element.x;
            ctx.save();
            ctx.strokeStyle = cssVar('--baseline');
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(x, area.top);
            ctx.lineTo(x, area.bottom);
            ctx.stroke();
            ctx.restore();
        }
    };

    // Draws each horizontal bar's value at its tip, in text ink (never the
    // bar's own hue) — see marks-and-anatomy.md ("Bars → value at the tip").
    function tipLabelPlugin(formatValue) {
        return {
            id: 'copilotTipLabels',
            afterDatasetsDraw: function (chart) {
                var meta = chart.getDatasetMeta(0);
                var data = chart.data.datasets[0].data;
                var ctx = chart.ctx;
                ctx.save();
                ctx.fillStyle = cssVar('--text-secondary');
                ctx.font = '12px system-ui, -apple-system, "Segoe UI", sans-serif';
                ctx.textBaseline = 'middle';
                ctx.textAlign = 'left';
                meta.data.forEach(function (bar, i) {
                    ctx.fillText(formatValue(data[i]), bar.x + 6, bar.y);
                });
                ctx.restore();
            }
        };
    }

    function renderTrendLine(canvasId, dataScriptId, colorVar, seriesLabel, opts) {
        opts = opts || {};
        var payload = readJsonData(dataScriptId);
        var canvas = document.getElementById(canvasId);
        if (!payload || !canvas) {
            return null;
        }

        var color = cssVar('--' + colorVar);
        var n = payload.values.length;
        var pointRadius = payload.values.map(function (_, i) { return i === n - 1 ? 4 : 0; });
        var pointHoverRadius = payload.values.map(function () { return 4; });

        return new Chart(canvas.getContext('2d'), {
            type: 'line',
            data: {
                labels: payload.labels,
                datasets: [{
                    label: seriesLabel,
                    data: payload.values,
                    borderColor: color,
                    backgroundColor: color,
                    borderWidth: 2,
                    borderJoinStyle: 'round',
                    borderCapStyle: 'round',
                    pointRadius: pointRadius,
                    pointHoverRadius: pointHoverRadius,
                    pointBackgroundColor: color,
                    pointBorderColor: cssVar('--surface-1'),
                    pointBorderWidth: 2,
                    fill: false,
                    tension: 0.15
                }]
            },
            options: {
                plugins: {
                    legend: { display: false }, // single series — title already names it
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                        callbacks: {
                            label: function (ctx) {
                                var v = ctx.parsed.y;
                                return opts.currency
                                    ? new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD' }).format(v)
                                    : new Intl.NumberFormat().format(v);
                            }
                        }
                    }
                },
                interaction: { mode: 'index', intersect: false },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { color: cssVar('--text-muted') }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: cssVar('--gridline') },
                        border: { display: false },
                        ticks: {
                            color: cssVar('--text-muted'),
                            callback: function (v) {
                                return opts.currency ? '$' + v : v;
                            }
                        }
                    }
                }
            },
            plugins: [crosshairPlugin]
        });
    }

    function renderMemberBar(canvasId, dataScriptId) {
        var payload = readJsonData(dataScriptId);
        var canvas = document.getElementById(canvasId);
        if (!payload || !canvas) {
            return null;
        }

        var color = cssVar('--series-1');

        return new Chart(canvas.getContext('2d'), {
            type: 'bar',
            data: {
                labels: payload.labels,
                datasets: [{
                    data: payload.values,
                    backgroundColor: color,
                    borderRadius: 4,
                    borderSkipped: false,
                    maxBarThickness: 24
                }]
            },
            options: {
                indexAxis: 'y',
                layout: { padding: { right: 48 } },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                return new Intl.NumberFormat().format(ctx.parsed.x) + ' requests';
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        grid: { color: cssVar('--gridline') },
                        border: { display: false },
                        ticks: { color: cssVar('--text-muted') }
                    },
                    y: {
                        grid: { display: false },
                        ticks: { color: cssVar('--text-primary') }
                    }
                }
            },
            plugins: [tipLabelPlugin(function (v) { return new Intl.NumberFormat().format(v); })]
        });
    }

    global.CopilotCharts = {
        renderTrendLine: renderTrendLine,
        renderMemberBar: renderMemberBar
    };
})(window);
