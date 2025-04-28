document.addEventListener('htmx:configRequest', function (event) {
  // Add the global state value to the request headers
  const globalStateInput = document.querySelector('input[name="global_state"]');

  if (!globalStateInput) return;

  const globalStateValue = globalStateInput.value;

  if (!globalStateValue) return;

  event.detail.headers['X-Global-State'] = globalStateValue;
});
