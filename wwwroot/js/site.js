(function () {
    "use strict";

    // Mobile sidebar toggle
    document.querySelectorAll("[data-toggle-sidebar]").forEach(btn =>
        btn.addEventListener("click", () => document.getElementById("sidebar")?.classList.toggle("open")));

    // Property card image galleries
    document.querySelectorAll("[data-gallery]").forEach(gallery => {
        const images = gallery.querySelectorAll("img");
        const dots = gallery.querySelectorAll(".gallery-dots span");
        let index = 0;
        const show = i => {
            index = (i + images.length) % images.length;
            images.forEach((img, n) => img.classList.toggle("show", n === index));
            dots.forEach((d, n) => d.classList.toggle("on", n === index));
        };
        gallery.querySelector(".prev")?.addEventListener("click", e => { e.preventDefault(); e.stopPropagation(); show(index - 1); });
        gallery.querySelector(".next")?.addEventListener("click", e => { e.preventDefault(); e.stopPropagation(); show(index + 1); });
        show(0);
    });

    // Booking widget: live price preview (server recalculates on submit)
    const widget = document.querySelector("[data-booking-widget]");
    if (widget) {
        const price = parseFloat(widget.dataset.price);
        const cleaning = parseFloat(widget.dataset.cleaning);
        const feeRate = parseFloat(widget.dataset.feeRate);
        const maxGuests = parseInt(widget.dataset.maxGuests, 10);
        const checkIn = widget.querySelector("[name=CheckInDate]");
        const checkOut = widget.querySelector("[name=CheckOutDate]");
        const guests = widget.querySelector("[name=NumberOfGuests]");
        const money = v => "R " + Math.round(v).toLocaleString("en-US").replace(/,/g, " ");

        const update = () => {
            const nights = Math.round((new Date(checkOut.value) - new Date(checkIn.value)) / 86400000);
            const breakdown = widget.querySelector("[data-breakdown]");
            if (!(nights > 0)) { breakdown.hidden = true; return; }
            const subtotal = price * nights;
            const fee = Math.round(subtotal * feeRate * 100) / 100;
            widget.querySelector("[data-nights-label]").textContent = `${money(price)} x ${nights} night${nights === 1 ? "" : "s"}`;
            widget.querySelector("[data-subtotal]").textContent = money(subtotal);
            widget.querySelector("[data-cleaning]").textContent = money(cleaning);
            widget.querySelector("[data-fee]").textContent = money(fee);
            widget.querySelector("[data-total]").textContent = money(subtotal + cleaning + fee);
            breakdown.hidden = false;
        };

        checkIn.addEventListener("change", () => {
            if (checkOut.value <= checkIn.value) {
                const next = new Date(checkIn.value);
                next.setDate(next.getDate() + 1);
                checkOut.value = next.toISOString().slice(0, 10);
            }
            checkOut.min = checkIn.value;
            update();
        });
        checkOut.addEventListener("change", update);
        widget.querySelectorAll("[data-step]").forEach(btn => btn.addEventListener("click", () => {
            const v = Math.min(maxGuests, Math.max(1, (parseInt(guests.value, 10) || 1) + parseInt(btn.dataset.step, 10)));
            guests.value = v;
        }));
        update();
    }

    // Image file previews
    document.querySelectorAll("input[type=file][data-preview]").forEach(input => {
        const target = document.querySelector(input.dataset.preview);
        input.addEventListener("change", () => {
            target.innerHTML = "";
            [...input.files].forEach(file => {
                if (!file.type.startsWith("image/")) {
                    const span = document.createElement("span");
                    span.className = "badge text-bg-light border";
                    span.textContent = file.name;
                    target.appendChild(span);
                    return;
                }
                const img = document.createElement("img");
                img.src = URL.createObjectURL(file);
                img.alt = file.name;
                target.appendChild(img);
            });
        });
    });

    // Check-in process step editor (host)
    const editor = document.querySelector("[data-step-editor]");
    if (editor) {
        const list = editor.querySelector("[data-step-list]");
        const template = editor.querySelector("#step-template");

        const reindex = () => list.querySelectorAll(".step-row").forEach((row, i) => {
            row.querySelector("[data-step-number]").textContent = i + 1;
            row.querySelectorAll("[data-field]").forEach(el => {
                el.name = `Steps[${i}].${el.dataset.field}`;
                if (el.type === "checkbox") el.value = "true";
            });
        });

        const wireRow = row => {
            row.querySelector("[data-remove]").addEventListener("click", () => { row.remove(); reindex(); });
            const toggle = row.querySelector("[data-field=RequiresDocument]");
            const docType = row.querySelector("[data-doc-type]");
            const sync = () => docType.hidden = !toggle.checked;
            toggle.addEventListener("change", sync);
            sync();
        };

        list.querySelectorAll(".step-row").forEach(wireRow);
        editor.querySelector("[data-add-step]").addEventListener("click", () => {
            const row = template.content.firstElementChild.cloneNode(true);
            list.appendChild(row);
            wireRow(row);
            reindex();
        });
        editor.closest("form").addEventListener("submit", reindex);
        reindex();
    }

    // Confirm dialogs for destructive actions
    document.querySelectorAll("form[data-confirm]").forEach(form =>
        form.addEventListener("submit", e => { if (!confirm(form.dataset.confirm)) e.preventDefault(); }));

    // Keep the message thread scrolled to the newest message
    const thread = document.querySelector("[data-thread]");
    if (thread) thread.scrollTop = thread.scrollHeight;
})();
