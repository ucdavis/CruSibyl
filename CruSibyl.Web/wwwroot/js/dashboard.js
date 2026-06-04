(function () {
  "use strict";

  const refreshTimers = new WeakMap();
  const chartInstances = new WeakMap();
  let chartLoadPromise = null;

  function initDashboard(root) {
    root = normalizeRoot(root);
    const dashboardRoot = findDashboardRoot(root);

    if (dashboardRoot) {
      startAutoRefresh(dashboardRoot);
    }

    initSparklines(root);
  }

  function destroyDashboard(root) {
    root = normalizeRoot(root);
    const dashboardRoot = findDashboardRoot(root);
    if (dashboardRoot) {
      stopAutoRefresh(dashboardRoot);
    }

    destroySparklines(root);
  }

  function normalizeRoot(root) {
    return root instanceof Element || root instanceof Document ? root : document;
  }

  function findDashboardRoot(root) {
    if (root instanceof Element && root.matches("[data-dashboard-root]")) {
      return root;
    }

    return root.querySelector?.("[data-dashboard-root]") || null;
  }

  function startAutoRefresh(dashboardRoot) {
    if (refreshTimers.has(dashboardRoot)) {
      return;
    }

    const timer = window.setInterval(function () {
      if (!document.body.contains(dashboardRoot)) {
        window.clearInterval(timer);
        refreshTimers.delete(dashboardRoot);
        return;
      }

      if (window.htmx) {
        htmx.trigger(dashboardRoot, "dashboard:refresh");
      }
    }, 60000);

    refreshTimers.set(dashboardRoot, timer);
  }

  function stopAutoRefresh(dashboardRoot) {
    const timer = refreshTimers.get(dashboardRoot);
    if (!timer) {
      return;
    }

    window.clearInterval(timer);
    refreshTimers.delete(dashboardRoot);
  }

  function initSparklines(root) {
    const canvases = Array.from(root.querySelectorAll?.("canvas[data-dashboard-sparkline]") || []);
    const uninitialized = canvases.filter(function (canvas) {
      return !chartInstances.has(canvas);
    });

    if (uninitialized.length === 0) {
      return;
    }

    loadChartJs().then(function () {
      uninitialized.forEach(function (canvas) {
        if (document.body.contains(canvas)) {
          renderSparkline(canvas);
        }
      });
    }).catch(function (error) {
      console.warn("Chart.js could not be loaded for dashboard sparklines.", error);
    });
  }

  function loadChartJs() {
    if (window.Chart) {
      return Promise.resolve();
    }

    if (chartLoadPromise) {
      return chartLoadPromise;
    }

    chartLoadPromise = new Promise(function (resolve, reject) {
      const script = document.createElement("script");
      script.src = "https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js";
      script.async = true;
      script.onload = resolve;
      script.onerror = reject;
      document.head.appendChild(script);
    });

    return chartLoadPromise;
  }

  function renderSparkline(canvas) {
    if (!window.Chart || chartInstances.has(canvas)) {
      return;
    }

    const values = (canvas.dataset.values || "")
      .split(",")
      .map(Number)
      .filter(function (value) {
        return !Number.isNaN(value);
      });

    if (values.length === 0) {
      return;
    }

    const chart = new Chart(canvas, {
      type: "line",
      data: {
        labels: values.map(function (_, index) {
          return index;
        }),
        datasets: [{
          data: values,
          borderColor: "rgb(255, 99, 132)",
          borderWidth: 1,
          fill: false,
          pointRadius: 0,
        }],
      },
      options: {
        responsive: false,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          x: { display: false },
          y: { display: false },
        },
      },
    });
    chartInstances.set(canvas, chart);
  }

  function destroySparklines(root) {
    const canvases = root instanceof Element && root.matches("canvas[data-dashboard-sparkline]")
      ? [root]
      : Array.from(root.querySelectorAll?.("canvas[data-dashboard-sparkline]") || []);

    canvases.forEach(function (canvas) {
      const chart = chartInstances.get(canvas);
      if (!chart) {
        return;
      }

      chart.destroy();
      chartInstances.delete(canvas);
    });
  }

  document.addEventListener("htmx-components:load", function (event) {
    initDashboard(event.detail.root);
  });

  document.addEventListener("htmx:beforeCleanupElement", function (event) {
    destroyDashboard(event.detail.elt);
  });

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () {
      initDashboard(document);
    }, { once: true });
  } else {
    initDashboard(document);
  }
})();
