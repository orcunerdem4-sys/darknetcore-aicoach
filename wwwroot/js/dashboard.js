document.addEventListener('DOMContentLoaded', function () {
    // 1. Initialize Calendar
    var calendarEl = document.getElementById('calendar');
    var calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'dayGridMonth',
        firstDay: 1, // Start on Monday
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
        expandRows: true,
        dayMaxEvents: true, // Prevent squashing, show "more" link
        height: 'auto',
        contentHeight: 600,
        longPressDelay: 350, // Mobile touch support
        dateClick: function(info) {
            showDayDetails(info.dateStr);
        },
        select: function (info) {
            const dateInput = document.getElementById('taskDate');
            if (dateInput) {
                let defaultDate = info.startStr;
                if (!info.startStr.includes("T")) {
                    defaultDate += "T09:00"; // default time
                }
                dateInput.value = defaultDate.substring(0, 16);
            }
            showDayDetails(info.startStr.split('T')[0]);
            var myModal = new bootstrap.Modal(document.getElementById('addTaskModal'));
            myModal.show();
        },
        eventClick: function (info) {
            showDayDetails(info.event.startStr.split('T')[0]);
            
            // Fill detail modal
            document.getElementById('taskDetailTitle').textContent = info.event.title;
            const dateStr = info.event.start.toLocaleString();
            document.getElementById('taskDetailDate').textContent = dateStr;
            
            const statusBadge = document.getElementById('taskDetailStatus');
            const isCompleted = info.event.extendedProps.isCompleted;
            statusBadge.textContent = isCompleted ? 'Tamamlandı' : 'Devam Ediyor';
            statusBadge.className = `badge ${isCompleted ? 'bg-success-subtle text-success' : 'bg-secondary-subtle text-secondary'}`;

            // Completion toggle
            const completeBtn = document.getElementById('taskDetailCompleteBtn');
            completeBtn.onclick = () => {
                toggleTaskComplete(info.event.id, !isCompleted);
            };
            completeBtn.textContent = isCompleted ? '↩️ Devam Ediyor İşaretle' : '✅ Tamamlandı İşaretle';

            // Deletion logic
            const deleteBtn = document.getElementById('taskDetailDeleteBtn');
            deleteBtn.onclick = () => {
                if (confirm(`'${info.event.title}' görevini silmek istediğine emin misin?`)) {
                    fetch('/Dashboard/DeleteTask/' + info.event.id, { method: 'POST' })
                        .then(r => {
                            if (r.ok) {
                                info.event.remove();
                                loadTaskList();
                                window.showToast("Görev silindi.");
                            }
                        });
                }
            };

            var myModal = new bootstrap.Modal(document.getElementById('taskDetailModal'));
            myModal.show();
        },
        windowResize: function(view) {
            if (window.innerWidth < 768) {
                calendar.setOption('headerToolbar', {
                    left: 'prev,next',
                    center: 'title',
                    right: 'dayGridMonth,timeGridDay'
                });
            } else {
                calendar.setOption('headerToolbar', {
                    left: 'prev,next today',
                    center: 'title',
                    right: 'dayGridMonth,timeGridWeek,timeGridDay'
                });
            }
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
                        window.showToast("Görev eklendi!");
                    } else {
                        window.showToast("Görev eklenirken hata oluştu.", "error");
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
                        <div class="d-flex align-items-center gap-2 pe-2 w-100">
                            <input class="form-check-input mb-0" type="checkbox" ${task.isCompleted ? 'checked' : ''} onchange="toggleTaskComplete('${task.id}', this.checked)" style="width:1.2rem; height:1.2rem; cursor:pointer;" title="Görevi Tamamla">
                            <div class="flex-grow-1" style="${task.isCompleted ? 'text-decoration: line-through; opacity: 0.7;' : ''}">
                                <h6 class="mb-0 text-dark fw-bold" style="font-size: 0.95rem;">${task.title}</h6>
                                <small class="text-secondary d-flex align-items-center mt-1" style="font-size: 0.8rem;">
                                    <i data-lucide="clock" size="14" class="me-1"></i>${formatRelativeTime(task.dueDate || task.start)}
                                </small>
                            </div>
                            <div class="d-flex flex-column align-items-end gap-1">
                                <span class="badge ${badgeClass}" style="min-width: 60px;">Öncelik: ${badgeLabel}</span>
                                <button type="button" class="btn btn-sm btn-light rounded-circle p-1" 
                                        data-bs-toggle="tooltip" 
                                        data-bs-placement="top" 
                                        data-bs-custom-class="custom-tooltip"
                                        title="Zorluk Seviyesi ${difficultyScore}/10: ${difficultyReason}">
                                    <i data-lucide="info" size="16" class="text-primary"></i>
                                </button>
                            </div>
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
            case 2: return 'Yüksek';
            case 1: return 'Orta';
            default: return 'Düşük';
        }
    }

    // Initial Load
    loadTaskList();

    // 4. AI Chat Logic with Context
    const chatInput = document.getElementById('chatInput');
    const chatSendBtn = document.getElementById('chatSendBtn');
    const chatMessages = document.getElementById('chatMessages');
    let activeContextIds = [];
    let widgetSessionId = null;

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
                sessionId: widgetSessionId,
                contextFileIds: activeContextIds
            })
        })
            .then(r => r.json())
            .then(data => {
                // Hide Typing Indicator
                if (indicator) indicator.classList.add('d-none');

                if (data.success) {
                    if (data.sessionId) widgetSessionId = data.sessionId;
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

    function showDayDetails(dateStr) {
        const panel = document.getElementById('dayDetailPanel');
        const display = document.getElementById('selectedDateDisplay');
        const text = document.getElementById('focusedDateText');
        const list = document.getElementById('dayTaskList');
        
        if(!panel || !list) return;

        panel.style.display = 'block';
        display.style.display = 'flex';
        text.textContent = formatDateTurkish(dateStr);
        window.focusedDate = dateStr;

        fetch('/Dashboard/GetTasks')
            .then(r => r.json())
            .then(tasks => {
                const dayTasks = tasks.filter(t => (t.dueDate || t.start).split('T')[0] === dateStr);
                list.innerHTML = '';
                
                if(dayTasks.length === 0) {
                    list.innerHTML = '<div class="text-center text-muted small py-4">Bu güne ait görev yok.</div>';
                    return;
                }

                dayTasks.forEach(task => {
                    const badgeClass = getPriorityBadge(task.priority);
                    const badgeLabel = getPriorityLabel(task.priority);
                    const div = document.createElement('div');
                    div.className = 'list-group-item bg-transparent border-0 border-bottom py-3 px-0';
                    div.innerHTML = `
                        <div class="d-flex align-items-center gap-2 mb-2">
                            <input class="form-check-input" type="checkbox" ${task.isCompleted ? 'checked' : ''} 
                                   onchange="toggleTaskComplete('${task.id}', this.checked)" style="width:1rem; height:1rem;">
                            <span class="fw-bold small ${task.isCompleted ? 'text-decoration-line-through opacity-50' : ''}">${task.title}</span>
                        </div>
                        <div class="d-flex justify-content-between align-items-center">
                            <span class="badge ${badgeClass}" style="font-size: 0.65rem;">${badgeLabel}</span>
                            <small class="text-muted" style="font-size: 0.7rem;">${new Date(task.dueDate || task.start).toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'})}</small>
                        </div>
                    `;
                    list.appendChild(div);
                });
            });
    }

    function formatDateTurkish(dateStr) {
        const date = new Date(dateStr);
        return date.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', weekday: 'long' });
    }
});

// Global toggle function
window.toggleTaskComplete = function (taskId, isCompleted) {
    const taskData = {
        id: taskId,
        isCompleted: isCompleted,
    };

    fetch('/Dashboard/ToggleTaskComplete', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(taskData)
    }).then(response => {
        if (response.ok) {
            window.location.reload(); // Reload to see streak updates
        }
    });
};
