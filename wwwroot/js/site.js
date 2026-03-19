document.addEventListener("DOMContentLoaded", () => {
    const eventCards = document.querySelectorAll(".ev-event-card");

    eventCards.forEach((card) => {
        const joinButton = card.querySelector("button");

        if (!joinButton) {
            return;
        }

        joinButton.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();

            if (card.classList.contains("ev-event-joined")) {
                return;
            }

            card.classList.add("ev-event-joined");
            joinButton.textContent = "Joined";
            joinButton.setAttribute("aria-label", "Joined event");
        });

        card.addEventListener("click", () => {
            joinButton.click();
        });
    });
});
