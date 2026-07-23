window.sierraAccessibility = {
  focusById(id) {
    const target = document.getElementById(id);
    if (target) {
      target.focus();
      target.scrollIntoView({ block: "start" });
    }
  }
};
