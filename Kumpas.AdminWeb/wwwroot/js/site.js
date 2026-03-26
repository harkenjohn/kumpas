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
});
