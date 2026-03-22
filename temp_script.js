
    const sessionIdInput = document.getElementById('currentSessionId');
    const chatMessages = document.getElementById('mainChatMessages');
    const chatInput = document.getElementById('mainChatInput');
    const sendBtn = document.getElementById('sendBtn');
    const attachBtn = document.getElementById('attachBtn');
    const chatFileInput = document.getElementById('chatFileInput');
    const contextInfo = document.getElementById('contextInfo');
    const mediaPreview = document.getElementById('attachedMediaPreview');
    const newChatBtn = document.getElementById('newChatBtn');
    const typingIndicator = document.getElementById('typingIndicator');

    let attachedFileIds = [];
    let transientPaths = [];

    // Optional Enter listener logic inline here to avoid JS crashing
    if(chatInput) {
        chatInput.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                try {
                    e.preventDefault();
                    sendMessage();
                } catch(err) { alert("Enter Error: " + err.message); }
            }
        });
    }

    if (chatMessages) {
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    // Universal Image Capture Support
    chatInput.addEventListener('paste', (e) => {
        const items = (e.clipboardData || e.originalEvent.clipboardData).items;
        for (let i = 0; i < items.length; i++) {
            if (items[i].type.indexOf('image') !== -1) {
                const blob = items[i].getAsFile();
                uploadTransient(blob);
            }
        }
    });

    chatInput.addEventListener('dragover', (e) => {
        e.preventDefault();
        chatInput.style.borderColor = 'var(--accent-primary)';
    });
    chatInput.addEventListener('dragleave', () => {
        chatInput.style.borderColor = '';
    });
    chatInput.addEventListener('drop', (e) => {
        e.preventDefault();
        chatInput.style.borderColor = '';
        if (e.dataTransfer.files && e.dataTransfer.files[0]) {
            const file = e.dataTransfer.files[0];
            if (file.type.startsWith('image/')) {
                uploadTransient(file);
            }
        }
    });

    async function uploadTransient(file) {
        const formData = new FormData();
        formData.append('file', file);
        try {
            const res = await fetch('/Dashboard/UploadTransientFile', { method: 'POST', body: formData });
            const data = await res.json();
            if (data.success) {
                transientPaths.push(data.filePath);
                renderMediaPreview(data.filePath, data.fileName);
            }
        } catch (e) { console.error('Upload error', e); }
    }

    function renderMediaPreview(path, name) {
        const div = document.createElement('div');
        div.className = 'position-relative';
        div.style.width = '50px';
        div.style.height = '50px';
        
        const isAudio = path.endsWith('.webm');
        const content = isAudio 
            ? `<div class="bg-secondary rounded d-flex align-items-center justify-content-center w-100 h-100 text-white"><i data-lucide="mic" size="16"></i></div>`
            : `<img src="${path}" class="rounded w-100 h-100 object-fit-cover shadow-sm border">`;
            
        div.innerHTML = `${content}
            <button onclick="removeTransient('${path}', this)" class="btn btn-danger btn-sm p-0 rounded-circle position-absolute" style="top:-5px; right:-5px; width:18px; height:18px; line-height:1;">&times;</button>`;
        mediaPreview.appendChild(div);
        lucide.createIcons();
    }

    window.removeTransient = function(path, btn) {
        transientPaths = transientPaths.filter(p => p !== path);
        btn.parentElement.remove();
    }

    // Attach File Logic
    if(attachBtn && chatFileInput) {
        attachBtn.addEventListener('click', () => chatFileInput.click());
        chatFileInput.addEventListener('change', function() {
            if (this.files.length) uploadTransient(this.files[0]);
            this.value = '';
        });
    }

    // Use marked defaults and add breaks
    try {
        marked.use({ breaks: true });
    } catch (e) {
        console.error("Marked Init Error:", e);
    }

    // Helper: always read current session id dynamically
    function getSessionId() { return sessionIdInput.value || null; }

    // Render existing markdown messages on load
    document.querySelectorAll('.bubble-content[data-markdown]').forEach(el => {
        el.innerHTML = marked.parse(el.dataset.markdown || el.textContent);
        renderCommandButtons(el);
    });

    function renderCommandButtons(parent) {
        if (parent.querySelector('.batch-approval-container')) return;
        
        const taskContainers = [];
        parent.querySelectorAll('.ai-command-container').forEach(container => {
            if (container.children.length > 0) return;
            const cmdData = container.dataset.command;
            const cmd = JSON.parse(cmdData);
            
            if (cmd.command === 'add_task') {
                taskContainers.push({ container, cmdData });
            }
        });

        if (taskContainers.length > 0) {
            // Her görev satırındaki butonları küçültüp bilgi notu haline getir
            taskContainers.forEach((tc, index) => {
                tc.container.innerHTML = `<span class="text-muted small px-2 py-1 rounded bg-card border"><i data-lucide="calendar" size="14"></i> Planlanan Görev</span>`;
            });

            // En alta toplu onay kutusu ekle
            const batchDiv = document.createElement('div');
            batchDiv.className = 'batch-approval-container mt-3 p-2 rounded flex-wrap gap-2 d-flex align-items-center justify-content-between';
            batchDiv.style.background = 'var(--bg-input)';
            batchDiv.style.border = '1px solid var(--border-dim)';
            
            batchDiv.innerHTML = `
                <span class="small fw-bold text-dark"><i data-lucide="layers" size="16" class="me-1"></i> ${taskContainers.length} Görev Planı Hazır</span>
                <div class="d-flex gap-2">
                    <button class="btn-approval btn-approve py-1 px-3 batch-approve-btn">
                        <i data-lucide="check" size="14"></i> Tümünü Onayla
                    </button>
                    <button class="btn-approval btn-reject py-1 px-3 batch-reject-btn">
                        <i data-lucide="x" size="14"></i> Reddet
                    </button>
                </div>
            `;
            parent.appendChild(batchDiv);
            
            const btnApprove = batchDiv.querySelector('.batch-approve-btn');
            const btnReject = batchDiv.querySelector('.batch-reject-btn');
            
            btnApprove.addEventListener('click', async function() {
                const container = this.closest('.batch-approval-container');
                this.innerHTML = '<i class="spinner-border spinner-border-sm"></i> Bekleyin...';
                this.disabled = true;
                btnReject.disabled = true;
                
                try {
                    let successCount = 0;
                    for (let cmdJsonStr of taskContainers.map(t => t.cmdData)) {
                        const res = await fetch('/Dashboard/ExecuteAiCommand', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: cmdJsonStr
                        });
                        const data = await res.json();
                        if (data.success) successCount++;
                    }
                    
                    container.innerHTML = \`<span class="text-success small fw-bold"><i data-lucide="check-circle" size="14"></i> \${successCount} Görev Takvime Eklendi</span>\`;
                    lucide.createIcons();
                } catch (e) {
                    alert('Hata: ' + e.message);
                    this.disabled = false;
                    btnReject.disabled = false;
                    this.innerHTML = '<i data-lucide="check" size="14"></i> Tümünü Onayla';
                    lucide.createIcons();
                }
            });
            
            btnReject.addEventListener('click', function() {
                const container = this.closest('.batch-approval-container');
                container.innerHTML = \`<span class="text-muted small italic"><i data-lucide="info" size="14"></i> Plan reddedildi.</span>\`;
                lucide.createIcons();
            });

            lucide.createIcons();
        }
    }

    async function showFilePreview(fileId) {
        const modal = document.getElementById('filePreviewModal');
        const overlay = document.getElementById('sidebarDimOverlay');
        const bodyContent = document.getElementById('previewFileSummary');
        
        modal.classList.add('open');
        overlay.classList.add('open');
        bodyContent.innerHTML = '<div class="text-center p-3"><div class="spinner-border spinner-border-sm text-success"></div></div>';
        
        try {
            const res = await fetch('/Dashboard/GetFilePreview?id=' + fileId);
            const data = await res.json();
            
            document.getElementById('previewFileName').textContent = data.name;
            document.getElementById('previewFileType').textContent = data.type;
            document.getElementById('previewFileDate').textContent = data.date;
            
            let html = '';
            const filePath = data.path || data.url;

            if (data.type === 'Image') {
                html = `<img src="${filePath}" class="img-fluid rounded shadow-sm mb-3" style="max-height: 300px; width: 100%; object-fit: contain;">`;
            } else if (data.type === 'Document' && (filePath.endsWith('.pdf'))) {
                html = `<div class="mb-3 text-center">
                            <i data-lucide="file-text" size="48" class="text-muted mb-2"></i>
                            <p class="small text-muted">PDF dökümanı hazır.</p>
                            <a href="${filePath}" target="_blank" class="btn btn-sm btn-outline-success">Tam Ekran Aç</a>
                        </div>`;
            } else {
                html = `<div class="mb-3 text-center"><i data-lucide="file" size="48" class="text-muted mb-2"></i></div>`;
            }

            html += `<div class="preview-summary-text small mt-2">${data.summary || 'Özet yok.'}</div>`;
            html += `<div class="mt-3"><a href="${filePath}" download class="btn btn-sm btn-success w-100">Dosyayı İndir</a></div>`;
            
            bodyContent.innerHTML = html;
            lucide.createIcons();
        } catch (e) {
            bodyContent.innerHTML = '<div class="text-danger small">Hata: Önizleme yüklenemedi.</div>';
        }
    }

    function closeFilePreview() {
        document.getElementById('filePreviewModal').classList.remove('open');
        if (!document.getElementById('chatSidebar').classList.contains('sidebar-open')) {
            document.getElementById('sidebarDimOverlay').classList.remove('open');
        }
    }

    // Auto-scroll to bottom
    function scrollToBottom() {
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }
    scrollToBottom();

    // Auto-resize textarea
    chatInput.addEventListener('input', function () {
        this.style.height = 'auto';
        this.style.height = Math.min(this.scrollHeight, 120) + 'px';
    });

    // Suggestion chips
    document.querySelectorAll('.suggestion-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            chatInput.value = btn.textContent.trim().replace(/^[^\w]+/, '');
            sendMessage();
        });
    });

    // ── Voice Message Logic ──────────────────────────────────
    const voiceBtn = document.getElementById('voiceBtn');
    const micIcon = document.getElementById('micIcon');
    const inputPillContainer = document.getElementById('inputPillContainer');
    let mediaRecorder;
    let audioChunks = [];
    let isRecording = false;

    if (voiceBtn) {
        voiceBtn.addEventListener('click', async () => {
            if (!isRecording) {
                try {
                    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                    mediaRecorder = new MediaRecorder(stream);
                    audioChunks = [];

                    mediaRecorder.ondataavailable = (event) => audioChunks.push(event.data);
                    mediaRecorder.onstop = async () => {
                        const audioBlob = new Blob(audioChunks, { type: 'audio/webm' });
                        uploadTransient(audioBlob);
                    };

                    mediaRecorder.start();
                    isRecording = true;
                    micIcon.classList.add('recording-pulse');
                    inputPillContainer.classList.add('recording-active');
                } catch (err) { alert('Mikrofon erişimi reddedildi.'); }
            } else {
                mediaRecorder.stop();
                mediaRecorder.stream.getTracks().forEach(track => track.stop());
                isRecording = false;
                micIcon.classList.remove('recording-pulse');
                inputPillContainer.classList.remove('recording-active');
            }
        });
    }

    // New chat
    document.getElementById('newChatBtn').addEventListener('click', async function() {
        const btn = this;
        const originalHtml = btn.innerHTML;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm border-success" role="status" aria-hidden="true" style="width: 14px; height: 14px;"></span>';
        btn.disabled = true;

        try {
            const res = await fetch('/Dashboard/NewChatSession', { method: 'POST' });
            if (!res.ok) throw new Error('HTTP ' + res.status);
            
            const data = await res.json();
            if (data.success) {
                window.location.href = '/Dashboard/Chat?sessionId=' + data.sessionId;
            } else {
                alert("Hata: " + (data.message || "Yeni sohbet sayfası yüklenirken bir sorun oluştu."));
                btn.innerHTML = originalHtml;
                btn.disabled = false;
            }
        } catch (e) {
            console.error("New chat error: ", e);
            alert("Beklenmeyen bir hata oluştu: Bağlantınızı kontrol edin.");
            btn.innerHTML = originalHtml;
            btn.disabled = false;
        }
    });

    // ── Mobile Sidebar Toggle ──────────────────────────────────
    const chatSidebar = document.getElementById('chatSidebar');
    const sidebarDimOverlay = document.getElementById('sidebarDimOverlay');
    const sidebarToggleBtn = document.getElementById('sidebarToggleBtn');

    function openSidebar() {
        chatSidebar?.classList.add('sidebar-open');
        sidebarDimOverlay?.classList.add('open');
        document.body.style.overflow = 'hidden';
    }
    function closeSidebar() {
        chatSidebar?.classList.remove('sidebar-open');
        sidebarDimOverlay?.classList.remove('open');
        document.body.style.overflow = '';
    }

    if (sidebarToggleBtn) sidebarToggleBtn.addEventListener('click', openSidebar);
    if (sidebarDimOverlay) sidebarDimOverlay.addEventListener('click', () => {
        closeSidebar();
        closeFilePreview();
    });

    function appendMessage(role, content) {
        try {
            const wrapper = document.createElement('div');
            wrapper.className = `chat-bubble-wrapper ${role === 'user' ? 'user-bubble-wrapper' : 'ai-bubble-wrapper'} mb-3`;

            const timeStr = new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });

            if (role !== 'user') {
                const avatar = document.createElement('div');
                avatar.className = 'ai-avatar-sm me-3 mt-1';
                avatar.style.cssText = 'width:24px;height:24px;border-radius:50%;background:linear-gradient(135deg, #4285f4, #9b72cb);display:flex;align-items:center;justify-content:center;flex-shrink:0;';
                avatar.innerHTML = '<i data-lucide="sparkles" size="14" style="color:white;"></i>';
                wrapper.appendChild(avatar);
            }

            const bubble = document.createElement('div');
            bubble.className = `chat-bubble ${role === 'user' ? 'user-bubble' : 'ai-bubble'}`;

            const bubbleContent = document.createElement('div');
            bubbleContent.className = 'bubble-content';
            try {
                bubbleContent.innerHTML = role === 'user' ? escapeHtml(content) : (typeof marked !== 'undefined' ? marked.parse(content) : escapeHtml(content));
            } catch(ee) { bubbleContent.innerText = content; }

            const bubbleTime = document.createElement('div');
            bubbleTime.className = 'bubble-time';
            bubbleTime.textContent = timeStr;

            bubble.appendChild(bubbleContent);
            bubble.appendChild(bubbleTime);
            wrapper.appendChild(bubble);

            const es = document.getElementById('emptyState');
            if(es) es.remove();

            if(chatMessages && typingIndicator) {
                chatMessages.insertBefore(wrapper, typingIndicator);
            } else if(chatMessages) {
                chatMessages.appendChild(wrapper);
            }

            if (role !== 'user') {
                requestAnimationFrame(() => {
                    wrapper.scrollIntoView({ behavior: 'smooth', block: 'start' });
                });
            } else {
                scrollToBottom();
            }

            if(typeof lucide !== 'undefined') lucide.createIcons();
            return wrapper;
        } catch (err) {
            console.error("appendMessage error:", err);
            alert("Sohbet baloncuğu oluşturulurken hata: " + err.message);
            return null;
        }
    }

    function escapeHtml(text) {
        return text.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
    }

    function getIntensityMode() {
        const checked = document.querySelector('input[name="intensity"]:checked');
        return checked ? checked.value : 'Normal';
    }

    async function sendMessage() {
        try {
            const msg = (chatInput && chatInput.value) ? chatInput.value.trim() : '';
            if (!msg && attachedFileIds.length === 0 && transientPaths.length === 0) return;

            let fallbackStr = "📷 Görsel gönderildi";
            if (transientPaths.some(p => p.endsWith('.webm'))) fallbackStr = "🎤 Sesli mesaj gönderildi";
            
            appendMessage('user', msg || fallbackStr);
            
            if(chatInput) {
                chatInput.value = '';
                chatInput.style.height = 'auto';
            }

            if(typingIndicator) typingIndicator.classList.remove('d-none');
            scrollToBottom();
            if(sendBtn) sendBtn.disabled = true;

            const response = await fetch('/Dashboard/ChatWithAi', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    message: msg,
                    sessionId: getSessionId(),
                    intensityMode: getIntensityMode(),
                    contextFileIds: attachedFileIds,
                    transientPaths: transientPaths
                })
            });
            const data = await response.json();
                if (data.success) {
                    const assistantMsg = appendMessage('assistant', data.response);
                    if(assistantMsg) renderCommandButtons(assistantMsg.querySelector('.bubble-content'));
                    if (data.sessionId && sessionIdInput) {
                        sessionIdInput.value = data.sessionId;
                        history.replaceState(null, '', '/Dashboard/Chat?sessionId=' + data.sessionId);
                    }
                } else {
                    appendMessage('assistant', '⚠️ ' + (data.response || 'Bir hata oluştu.'));
                }
                
                // Reset media after send
                attachedFileIds = [];
                transientPaths = [];
                if(mediaPreview) mediaPreview.innerHTML = '';
                if(contextInfo) contextInfo.textContent = '';
                
            } catch (err) {
                console.error("sendMessage error:", err);
                alert("Mesaj gönderilirken sunucu hatası: " + err.message);
                if(typingIndicator) typingIndicator.classList.add('d-none');
            } finally {
                if(typingIndicator) typingIndicator.classList.add('d-none');
                if(sendBtn) sendBtn.disabled = false;
            }
    }

    // (Event listeners moved to the top of script to guarantee attachment)
