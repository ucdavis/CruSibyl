(function () {
  "use strict";

  function findRoot(element) {
    return element.closest("[data-error-test-root]");
  }

  function findErrorRegion(root) {
    return root?.querySelector("htmx-error-region, [data-hc-error-region]");
  }

  function clearError(root) {
    const region = findErrorRegion(root);

    if (!region) {
      return;
    }

    region.hidden = true;
    region.replaceChildren();
  }

  function dispatchHtmxEvent(element, name) {
    const root = findRoot(element);

    element.dispatchEvent(new CustomEvent(name, {
      bubbles: true,
      cancelable: true,
      detail: {
        elt: element,
        target: root,
        requestConfig: {
          path: "/dev/htmx-errors/client",
        },
      },
    }));
  }

  document.addEventListener("click", function (event) {
    const button = event.target instanceof Element
      ? event.target.closest("[data-error-test-action]")
      : null;

    if (!(button instanceof Element)) {
      return;
    }

    const action = button.getAttribute("data-error-test-action");

    if (action === "clear") {
      clearError(findRoot(button));
      return;
    }

    if (action === "send-error") {
      dispatchHtmxEvent(button, "htmx:sendError");
      return;
    }

    if (action === "swap-error") {
      dispatchHtmxEvent(button, "htmx:swapError");
    }
  });
})();
