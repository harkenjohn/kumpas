document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-confirm]").forEach((element) => {
        element.addEventListener("submit", (event) => {
            const message = element.getAttribute("data-confirm") || "Are you sure?";
            if (!window.confirm(message)) {
                event.preventDefault();
            }
        });
    });

    document.querySelectorAll("[data-bs-toggle='collapse']").forEach((button) => {
        button.addEventListener("click", () => {
            button.classList.toggle("active");
        });
    });

    document.querySelectorAll("[data-toggle-uptime]").forEach((button) => {
        button.addEventListener("click", () => {
            const graph = document.getElementById("model-uptime-graph");
            if (!graph) {
                return;
            }

            graph.hidden = !graph.hidden;
            button.textContent = graph.hidden ? "View Uptime Graph" : "Hide Uptime Graph";

            if (!graph.hidden) {
                graph.scrollIntoView({ behavior: "smooth", block: "start" });
            }
        });
    });
});
