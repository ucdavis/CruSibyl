document.addEventListener("htmx:afterSettle", () => {
  // detects if table is in edit mode and disables all parts except the row being edited...
  const toggle = document.getElementById("table-edit-class-toggle");
  const wrapper = document.getElementById("table-container");

  if (toggle && wrapper) {
    const editing = toggle.classList.contains("editing-mode");
    wrapper.classList.toggle("editing-mode", editing);
  }
});
