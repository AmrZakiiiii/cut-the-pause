/*
  Deployment configuration: set these values only when a reviewed URL exists.
  Checkout stays empty until a reviewed payment URL exists. Release links are
  enabled only after the public beta artifact is available.
*/
window.CUT_THE_PAUSE_CONFIG = window.CUT_THE_PAUSE_CONFIG || {
  releaseUrl: "https://github.com/AmrZakiiiii/cut-the-pause/releases/tag/v0.1.0-beta",
  checkoutUrl: ""
};

(function configureSafeLinks() {
  "use strict";

  var config = window.CUT_THE_PAUSE_CONFIG;
  var releaseLinks = document.querySelectorAll("[data-release-link]");
  var checkoutButton = document.querySelector("[data-checkout-link]");
  var status = document.getElementById("download-status");

  function isUsableUrl(value) {
    return typeof value === "string" && /^(https?:|mailto:)/i.test(value.trim());
  }

  if (isUsableUrl(config.releaseUrl)) {
    releaseLinks.forEach(function (link) {
      link.href = config.releaseUrl.trim();
      link.removeAttribute("aria-disabled");
      link.addEventListener("click", function () { link.removeAttribute("aria-disabled"); });
    });
    if (status) status.textContent = "Release link configured for this deployment.";
  } else {
    releaseLinks.forEach(function (link) {
      link.setAttribute("aria-disabled", "true");
      link.addEventListener("click", function (event) {
        event.preventDefault();
        var target = document.getElementById("download");
        var reducedMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        if (target) target.scrollIntoView({ behavior: reducedMotion ? "auto" : "smooth", block: "start" });
        if (status) status.textContent = "No public beta link is configured for this deployment.";
      });
    });
  }

  if (checkoutButton && isUsableUrl(config.checkoutUrl)) {
    checkoutButton.disabled = false;
    checkoutButton.textContent = "Continue to checkout";
    checkoutButton.addEventListener("click", function () { window.location.href = config.checkoutUrl.trim(); });
  }
})();
