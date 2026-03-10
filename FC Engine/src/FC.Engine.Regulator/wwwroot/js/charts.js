(function () {
    const chartRegistry = window.__fcCharts || (window.__fcCharts = {});

    window.renderChart = function (canvasId, type, data) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === "undefined") {
            return;
        }

        const ctx = canvas.getContext("2d");
        if (!ctx) {
            return;
        }

        if (chartRegistry[canvasId]) {
            chartRegistry[canvasId].destroy();
        }

        chartRegistry[canvasId] = new Chart(ctx, {
            type: type,
            data: {
                labels: data.labels || [],
                datasets: (data.datasets || []).map(function (dataset, index) {
                    const palette = [
                        "#0f766e",
                        "#1d4ed8",
                        "#d97706",
                        "#b91c1c",
                        "#7c3aed",
                        "#0ea5e9"
                    ];

                    const color = dataset.borderColor || dataset.backgroundColor || palette[index % palette.length];
                    const fill = type === "line" ? false : true;

                    return {
                        label: dataset.label,
                        data: dataset.data || [],
                        borderColor: color,
                        backgroundColor: dataset.backgroundColor || color,
                        borderWidth: 2,
                        tension: 0.3,
                        fill: fill
                    };
                })
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: "bottom"
                    }
                },
                scales: type !== "doughnut" ? {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: "#E2E8F0"
                        }
                    },
                    x: {
                        grid: {
                            display: false
                        }
                    }
                } : undefined
            }
        });
    };

    window.portalDownloadBase64File = function (base64Content, filename, contentType) {
        const binary = atob(base64Content);
        const bytes = new Uint8Array(binary.length);

        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }

        const blob = new Blob([bytes], { type: contentType });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = filename;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        URL.revokeObjectURL(url);
    };
})();
