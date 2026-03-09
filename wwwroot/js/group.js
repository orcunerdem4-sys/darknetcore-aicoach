document.addEventListener('DOMContentLoaded', () => {
    lucide.createIcons();

    const addNoteBtn = document.getElementById('addNoteBtn');
    const newNoteInput = document.getElementById('newNoteInput');
    const notesList = document.getElementById('notesList');

    const chatInput = document.getElementById('chatInput');
    const sendBtn = document.getElementById('sendBtn');
    const chatContainer = document.getElementById('groupChatContainer');

    function scrollToBottom() {
        if (chatContainer) {
            chatContainer.scrollTop = chatContainer.scrollHeight;
        }
    }

    // Initial scroll
    scrollToBottom();

    // Notes Logic
    if (addNoteBtn && newNoteInput) {
        addNoteBtn.addEventListener('click', async () => {
            const content = newNoteInput.value.trim();
            if (!content) return;

            addNoteBtn.disabled = true;
            try {
                const res = await fetch('/Group/AddNote', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(content)
                });

                const data = await res.json();
                if (data.success) {
                    newNoteInput.value = '';
                    window.showToast("Not başarıyla paylaşıldı!");

                    // Remove empty message if present
                    const emptyMsg = document.getElementById('emptyNotesMsg');
                    if (emptyMsg) emptyMsg.remove();

                    // Prepend new note
                    const noteHtml = `
                        <div class="card note-card bg-light border-0" id="note-${data.id}">
                            <div class="card-body p-3">
                                <div class="d-flex justify-content-between align-items-start mb-2">
                                    <small class="fw-bold text-primary">${data.author}</small>
                                    <button class="btn btn-link text-danger p-0 delete-note-btn" data-id="${data.id}">
                                        <i data-lucide="trash" size="14"></i>
                                    </button>
                                </div>
                                <p class="mb-1 text-dark" style="font-size: 0.9rem;">${content}</p>
                                <small class="text-muted" style="font-size: 0.7rem;">${data.date}</small>
                            </div>
                        </div>
                    `;
                    notesList.insertAdjacentHTML('afterbegin', noteHtml);
                    lucide.createIcons();
                } else {
                    window.showToast("Not eklenemedi.", "error");
                }
            } catch (err) {
                window.showToast("Bağlantı hatası oluştu.", "error");
            } finally {
                addNoteBtn.disabled = false;
            }
        });

        newNoteInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') addNoteBtn.click();
        });
    }

    // Delete Note Logic
    if (notesList) {
        notesList.addEventListener('click', async (e) => {
            const btn = e.target.closest('.delete-note-btn');
            if (!btn) return;

            if (!confirm("Bu notu silmek istediğinize emin misiniz?")) return;

            const id = btn.getAttribute('data-id');
            try {
                const res = await fetch('/Group/DeleteNote', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: `id=${id}`
                });
                const data = await res.json();
                if (data.success) {
                    document.getElementById(`note-${id}`)?.remove();
                    window.showToast("Not silindi.");
                } else {
                    window.showToast("Silinemedi.", "error");
                }
            } catch (err) {
                window.showToast("Bağlantı hatası.", "error");
            }
        });
    }

    // Chat Logic
    if (sendBtn && chatInput) {
        const sendMessage = async () => {
            const msg = chatInput.value.trim();
            if (!msg) return;

            chatInput.value = '';
            chatInput.disabled = true;
            sendBtn.disabled = true;

            // Optimistic UI for User
            const tempUserHtml = `
                <div class="chat-bubble user" style="opacity: 0.7;" id="tempMsg">
                    <div class="sender">Sen</div>
                    <div class="text">${msg}</div>
                    <div class="time">Gönderiliyor...</div>
                </div>
            `;
            chatContainer.insertAdjacentHTML('beforeend', tempUserHtml);
            scrollToBottom();

            // Typing block for AI
            const typingHtml = `
                <div class="chat-bubble ai mt-2" id="typingBlock">
                    <div class="sender"><i data-lucide="bot" size="12" class="me-1"></i>AI Coach</div>
                    <div class="text text-muted"><i data-lucide="loader" class="animate-spin" size="14"></i> Yazıyor...</div>
                </div>
            `;
            chatContainer.insertAdjacentHTML('beforeend', typingHtml);
            lucide.createIcons();
            scrollToBottom();

            try {
                const res = await fetch('/Group/SendMessage', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ message: msg })
                });

                document.getElementById('tempMsg')?.remove();
                document.getElementById('typingBlock')?.remove();

                const data = await res.json();
                if (data.success) {
                    // Render User Msg actual
                    const userHtml = `
                        <div class="chat-bubble user">
                            <div class="sender">${data.userMessage.sender}</div>
                            <div class="text">${data.userMessage.text.replace(/\n/g, "<br>")}</div>
                            <div class="time">${data.userMessage.time}</div>
                        </div>
                    `;
                    chatContainer.insertAdjacentHTML('beforeend', userHtml);

                    // Render AI Msg
                    const aiHtml = `
                        <div class="chat-bubble ai mt-2">
                            <div class="sender"><i data-lucide="bot" size="12" class="me-1"></i>${data.aiMessage.sender}</div>
                            <div class="text">${data.aiMessage.text.replace(/\n/g, "<br>")}</div>
                            <div class="time">${data.aiMessage.time}</div>
                        </div>
                    `;
                    chatContainer.insertAdjacentHTML('beforeend', aiHtml);
                    lucide.createIcons();
                    scrollToBottom();
                } else {
                    window.showToast("Mesaj gönderilemedi.", "error");
                }
            } catch (err) {
                document.getElementById('tempMsg')?.remove();
                document.getElementById('typingBlock')?.remove();
                window.showToast("Bağlantı hatası.", "error");
            } finally {
                chatInput.disabled = false;
                sendBtn.disabled = false;
                chatInput.focus();
            }
        };

        sendBtn.addEventListener('click', sendMessage);
        chatInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') sendMessage();
        });
    }
});

// Settings & Mute Actions
window.toggleGroupMute = async function () {
    try {
        const res = await fetch('/Group/ToggleGroupMute', { method: 'POST' });
        const data = await res.json();
        if (data.success) {
            window.showToast("Grup bildirim ayarı değiştirildi.", "success");
            // Basic UI toggle
            const btn = document.getElementById('muteGroupBtn');
            if (btn) btn.classList.toggle('active-mute');
        } else {
            window.showToast("İşlem başarısız oldu.", "error");
        }
    } catch (e) {
        window.showToast("Bağlantı hatası.", "error");
    }
}

window.toggleUserMute = async function (userId, userName) {
    if (!confirm(`${userName} kullanıcısından gelen bildirimleri susturmak/açmak istiyor musunuz?`)) return;

    try {
        const params = new URLSearchParams();
        params.append('mutedUserId', userId);

        const res = await fetch('/Group/ToggleUserMute', {
            method: 'POST',
            body: params
        });
        const data = await res.json();
        if (data.success) {
            window.showToast(`${userName} kullanıcısı için bildirim ayarı değiştirildi.`, "success");
        } else {
            window.showToast("Hata oluştu.", "error");
        }
    } catch (e) {
        window.showToast("Bağlantı hatası.", "error");
    }
}
