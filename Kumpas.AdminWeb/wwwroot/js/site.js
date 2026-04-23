document.addEventListener("DOMContentLoaded", () => {
    const confirmModal = document.getElementById("confirmModal");
    const confirmModalMessage = document.getElementById("confirmModalMessage");
    const confirmModalAccept = document.getElementById("confirmModalAccept");
    const confirmModalCancel = document.getElementById("confirmModalCancel");
    let pendingForm = null;

    const closeConfirmModal = () => {
        if (!confirmModal) {
            return;
        }

        confirmModal.hidden = true;
        pendingForm = null;
    };

    const openConfirmModal = (form, message) => {
        if (!confirmModal || !confirmModalMessage) {
            return;
        }

        pendingForm = form;
        confirmModalMessage.textContent = message;
        confirmModal.hidden = false;
        confirmModalAccept?.focus();
    };

    document.querySelectorAll("form[data-confirm]").forEach((form) => {
        form.addEventListener("submit", (event) => {
            if (form.dataset.confirmBypass === "true") {
                form.dataset.confirmBypass = "false";
                return;
            }

            event.preventDefault();
            const message = form.getAttribute("data-confirm") || "Are you sure you want to continue?";
            openConfirmModal(form, message);
        });
    });

    confirmModalAccept?.addEventListener("click", () => {
        if (!pendingForm) {
            closeConfirmModal();
            return;
        }

        pendingForm.dataset.confirmBypass = "true";
        pendingForm.requestSubmit();
        closeConfirmModal();
    });

    confirmModalCancel?.addEventListener("click", closeConfirmModal);

    confirmModal?.addEventListener("click", (event) => {
        if (event.target === confirmModal) {
            closeConfirmModal();
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && confirmModal && !confirmModal.hidden) {
            closeConfirmModal();
        }
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
