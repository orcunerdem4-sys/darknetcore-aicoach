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
        events: '/Dashboard/GetTasks', // Fetch tasks as events
        eventClick: function (info) {
            // Placeholder for edit modal
            alert('Task: ' + info.event.title);
        },
        editable: true,
        droppable: true
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
                        alert('Task added successfully!');
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
                    item.className = 'list-group-item list-group-item-action bg-transparent text-light border-secondary d-flex justify-content-between align-items-center';
                    item.innerHTML = `
                        <div>
                            <h6 class="mb-0">${task.title}</h6>
                            <small class="text-light opacity-50" style="font-size: 0.75rem;">
                                <i data-lucide="clock" size="12" class="me-1"></i>${formatRelativeTime(task.dueDate)}
                            </small>
                        </div>
                        <span class="badge ${getPriorityBadge(task.priority)}">${getPriorityLabel(task.priority)}</span>
                    `;
                    list.appendChild(item);
                });
                lucide.createIcons();
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
