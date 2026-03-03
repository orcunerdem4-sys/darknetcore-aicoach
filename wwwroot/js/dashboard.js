document.addEventListener('DOMContentLoaded', function () {
    // 1. Initialize Calendar
    var calendarEl = document.getElementById('calendar');
    var calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'dayGridMonth',
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: 'dayGridMonth,timeGridWeek,timeGridDay'
        },
        events: '/Dashboard/GetTasks',
        editable: true,
        droppable: true,
        selectable: true,
        selectMirror: true,
        // Boş bir yere (veya gün aralığına) tıklandğında
        select: function (info) {
            // "Zamanı" (info.startStr) taskDate inputuna doldur ve modal'ı manuel göster
            const dateInput = document.getElementById('taskDate');
            if (dateInput) {
                // Remove time part if it's allDay to prevent "invalid date" on datetime-local
                let defaultDate = info.startStr;
                if (!info.startStr.includes("T")) {
                    defaultDate += "T09:00"; // default time
                }
                dateInput.value = defaultDate.substring(0, 16); // format: YYYY-MM-DDTHH:mm
            }
            var myModal = new bootstrap.Modal(document.getElementById('addTaskModal'));
            myModal.show();
        },
        // Görevin üzerine fare ile gelince (X Butonu - Delete)
        eventMouseEnter: function (info) {
            const el = info.el;
            if (el.querySelector('.fc-event-delete-btn')) return;

            const btn = document.createElement('span');
            btn.className = 'fc-event-delete-btn bg-danger text-white rounded-circle d-flex align-items-center justify-content-center cursor-pointer shadow-sm';
            btn.innerHTML = '×';
            btn.style.position = 'absolute';
            btn.style.top = '-5px';
            btn.style.right = '-5px';
            btn.style.width = '20px';
            btn.style.height = '20px';
            btn.style.fontSize = '16px';
            btn.style.fontWeight = 'bold';
            btn.style.lineHeight = '1';
            btn.style.zIndex = '1000';
            btn.title = 'Sil';

            btn.onclick = function (e) {
                e.stopPropagation(); // Prevent event Click wrapper
                if (confirm(`'${info.event.title}' görevini takvimden silmek istediğine emin misin?`)) {
                    fetch('/Dashboard/DeleteTask/' + info.event.id, {
                        method: 'POST'
                    })
                        .then(response => {
                            if (response.ok) {
                                calendar.refetchEvents();
                                loadTaskList();
                            } else {
                                alert('Hata oluştu!');
                            }
                        });
                }
            };
            el.appendChild(btn);
        },
        // Fareden çıkınca X butonunu kaldır
        eventMouseLeave: function (info) {
            const btn = info.el.querySelector('.fc-event-delete-btn');
            if (btn) {
                // Silme butonunun üzerine gezinilirken de (örneğin buton sınırı taşmışsa) kapanmaması için gecikme/veya DOM'dan silme
                // Fakat pratiklik için doğrudan kaldırıyoruz
                btn.remove();
            }
        },
        // Göreve tıklanınca detay (Edit yapılabilir ama şimdilik alert)
        eventClick: function (info) {
            alert('Görev: ' + info.event.title);
        }
    });
    calendar.render();

    // 2. Handle Task Addition
    const addTaskForm = document.getElementById('addTaskForm');
    if (addTaskForm) {
        addTaskForm.addEventListener('submit', function (e) {
            e.preventDefault();

            const task = {
                title: document.getElementById('taskTitle').value,
                description: document.getElementById('taskDesc').value,
                dueDate: document.getElementById('taskDate').value,
                priority: parseInt(document.getElementById('taskPriority').value)
            };

            fetch('/Dashboard/CreateTask', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(task)
            })
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        calendar.refetchEvents(); // Refresh calendar
                        loadTaskList(); // Refresh list
                        addTaskForm.reset();

                        // Close Modal programmatically
                        var myModalEl = document.getElementById('addTaskModal');
                        if (myModalEl) {
                            var modalInstance = bootstrap.Modal.getInstance(myModalEl);
                            if (modalInstance) modalInstance.hide();
                        }
                    } else {
                        alert('Error adding task');
                    }
                });
        });
    }

    // 3. Load Task List (Side Panel)
    function loadTaskList() {
        fetch('/Dashboard/GetTasks')
            .then(response => response.json())
            .then(tasks => {
                const list = document.getElementById('taskList');
                list.innerHTML = '';
                tasks.forEach(task => {
                    const item = document.createElement('a');
                    item.className = 'list-group-item list-group-item-action bg-transparent border-secondary d-flex justify-content-between align-items-center mb-1 rounded';

                    // Priority Badge
                    const badgeClass = getPriorityBadge(task.priority);
                    const badgeLabel = getPriorityLabel(task.priority);

                    // Difficulty Tooltip
                    const difficultyScore = task.difficultyScore || 3;
                    const difficultyReason = task.difficultyReason || "Standart görev.";

                    item.innerHTML = `
                        <div class="pe-2">
                            <h6 class="mb-1 text-dark fw-bold" style="font-size: 0.95rem;">${task.title}</h6>
                            <small class="text-secondary d-flex align-items-center" style="font-size: 0.8rem;">
                                <i data-lucide="clock" size="14" class="me-1"></i>${formatRelativeTime(task.dueDate)}
                            </small>
                        </div>
                        <div class="d-flex align-items-center gap-2">
                            <span class="badge ${badgeClass}" style="min-width: 60px;">${badgeLabel}</span>
                            <button type="button" class="btn btn-sm btn-light rounded-circle p-1" 
                                    data-bs-toggle="tooltip" 
                                    data-bs-placement="top" 
                                    data-bs-custom-class="custom-tooltip"
                                    title="Zorluk ${difficultyScore}/10: ${difficultyReason}">
                                <i data-lucide="info" size="16" class="text-primary"></i>
                            </button>
                        </div>
                    `;
                    list.appendChild(item);
                });
                lucide.createIcons();

                // Initialize tooltips
                var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
                var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
                    return new bootstrap.Tooltip(tooltipTriggerEl);
                });
            });
    }

    function formatRelativeTime(dateString) {
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = date - now;
        const diffHrs = Math.floor(diffMs / (1000 * 60 * 60));
        const diffDays = Math.floor(diffHrs / 24);

        if (diffMs < 0) return "Overdue";
        if (diffHrs < 1) return "In < 1 hour";
        if (diffHrs < 24) return `In ${diffHrs} hours`;
        if (diffDays === 1) return `Tomorrow ${date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
        if (diffDays < 7) return date.toLocaleDateString([], { weekday: 'short', hour: '2-digit', minute: '2-digit' });

        return date.toLocaleDateString();
    }

    function getPriorityBadge(priority) {
        switch (priority) {
            case 2: return 'bg-danger';
            case 1: return 'bg-warning text-dark';
            default: return 'bg-success';
        }
    }

    function getPriorityLabel(priority) {
        switch (priority) {
            case 2: return 'High';
            case 1: return 'Medium';
            default: return 'Low';
        }
    }

    // Initial Load
    loadTaskList();

    // 4. AI Chat Logic with Context
    const chatInput = document.getElementById('chatInput');
    const chatSendBtn = document.getElementById('chatSendBtn');
    const chatMessages = document.getElementById('chatMessages');
    let activeContextIds = [];

    // Check for context from Files page
    const urlParams = new URLSearchParams(window.location.search);
    if (urlParams.get('context') === 'active') {
        const storedContext = localStorage.getItem('aiContextFiles');
        if (storedContext) {
            activeContextIds = JSON.parse(storedContext);
            if (activeContextIds.length > 0) {
                appendMessage('System', `Active Context: ${activeContextIds.length} files selected for planning.`);
                // Ideally fetch file names here too, but for now IDs confirm connection
            }
        }
    }

    function appendMessage(sender, text) {
        const msg = document.createElement('div');
        msg.className = `p-2 mb-2 rounded ${sender === 'User' ? 'bg-primary text-end' : sender === 'System' ? 'bg-info bg-opacity-25 text-center small' : 'bg-secondary'}`;
        msg.innerHTML = `<strong>${sender}:</strong> ${text}`;
        chatMessages.appendChild(msg);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    chatSendBtn.addEventListener('click', function () {
        sendMessage();
    });

    chatInput.addEventListener('keypress', function (e) {
        if (e.key === 'Enter') sendMessage();
    });

    function sendMessage() {
        const text = chatInput.value;
        if (!text) return;

        appendMessage('User', text);
        chatInput.value = '';

        // Show Typing Indicator
        const indicator = document.getElementById('chatTypingIndicator');
        if (indicator) {
            indicator.classList.remove('d-none');
            // Ensure icon is rendered if valid
            if (window.lucide) lucide.createIcons();
        }

        // Call backend with context
        fetch('/Dashboard/ChatWithAi', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                message: text,
                contextFileIds: activeContextIds
            })
        })
            .then(r => r.json())
            .then(data => {
                // Hide Typing Indicator
                if (indicator) indicator.classList.add('d-none');

                if (data.success) {
                    // Start with 'reply' (new backend format), fallback to 'response'
                    const finalResponse = data.reply || data.response;
                    appendMessage('AI', finalResponse);

                    if (data.actionPerformed === 'schedule_updated') {
                        calendar.refetchEvents();
                        loadTaskList();
                    }
                } else {
                    appendMessage('AI', 'Sorry, I encountered an error. ' + (data.response || ''));
                }
            })
            .catch(err => {
                console.error(err);
                if (indicator) indicator.classList.add('d-none');
                appendMessage('AI', 'Network error. Please try again.');
            });
    }
});
