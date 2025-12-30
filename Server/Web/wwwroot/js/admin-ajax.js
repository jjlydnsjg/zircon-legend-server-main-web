/**
 * 管理后台 AJAX 通用函数库
 * 用于处理表单提交和操作反馈,避免页面刷新
 */

// Toast 提示管理器
const ToastManager = {
    container: null,

    // 初始化 Toast 容器
    init() {
        if (!this.container) {
            this.container = document.createElement('div');
            this.container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            this.container.style.zIndex = '9999';
            document.body.appendChild(this.container);
        }
    },

    // 显示 Toast 消息
    show(message, type = 'info', duration = 3000) {
        this.init();

        const toastId = 'toast-' + Date.now();
        const iconMap = {
            success: 'check-circle',
            error: 'x-circle',
            warning: 'exclamation-triangle',
            info: 'info-circle'
        };
        const colorMap = {
            success: 'success',
            error: 'danger',
            warning: 'warning',
            info: 'primary'
        };

        const toastHTML = `
            <div id="${toastId}" class="toast show" role="alert">
                <div class="toast-header bg-${colorMap[type]} text-white">
                    <i class="bi bi-${iconMap[type]} me-2"></i>
                    <strong class="me-auto">操作结果</strong>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
                </div>
                <div class="toast-body">
                    ${message}
                </div>
            </div>
        `;

        this.container.insertAdjacentHTML('beforeend', toastHTML);
        const toastElement = document.getElementById(toastId);

        // 自动隐藏
        setTimeout(() => {
            const bsToast = new bootstrap.Toast(toastElement);
            bsToast.hide();
            setTimeout(() => toastElement.remove(), 500);
        }, duration);
    },

    success(message) {
        this.show(message, 'success');
    },

    error(message) {
        this.show(message, 'error', 5000);
    },

    warning(message) {
        this.show(message, 'warning');
    },

    info(message) {
        this.show(message, 'info');
    }
};

// AJAX 表单提交处理器
const AjaxForm = {
    /**
     * 提交表单数据
     * @param {HTMLFormElement} form - 表单元素
     * @param {Object} options - 配置选项
     * @returns {Promise}
     */
    submit(form, options = {}) {
        const defaults = {
            onSuccess: null,          // 成功回调
            onError: null,            // 错误回调
            successMessage: '操作成功',  // 成功提示
            showLoading: true,        // 是否显示加载状态
            resetForm: false,         // 成功后是否重置表单
            closeModal: false,        // 成功后是否关闭模态框
            modalId: null            // 模态框ID
        };

        const config = { ...defaults, ...options };
        const formData = new FormData(form);
        const action = form.action || window.location.href;
        const button = form.querySelector('button[type="submit"]');

        // 禁用提交按钮,显示加载状态
        if (config.showLoading && button) {
            button.disabled = true;
            const originalText = button.innerHTML;
            button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>处理中...';
            button.dataset.originalText = originalText;
        }

        return fetch(action, {
            method: 'POST',
            body: formData,
            headers: {
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value,
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'application/json, text/plain, */*'
            }
        })
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            return response.json().catch(() => {
                // 如果不是 JSON 响应,尝试解析为文本
                return { success: response.ok, message: '操作已完成' };
            });
        })
        .then(result => {
            // 恢复按钮状态
            if (config.showLoading && button) {
                button.disabled = false;
                button.innerHTML = button.dataset.originalText || button.innerHTML;
            }

            if (result.success) {
                // 显示成功消息
                ToastManager.success(result.message || config.successMessage);

                // 重置表单
                if (config.resetForm) {
                    form.reset();
                }

                // 关闭模态框
                if (config.closeModal && config.modalId) {
                    const modal = bootstrap.Modal.getInstance(document.getElementById(config.modalId));
                    if (modal) modal.hide();
                }

                // 执行成功回调
                if (config.onSuccess) {
                    config.onSuccess(result);
                }
            } else {
                // 显示错误消息
                ToastManager.error(result.message || '操作失败');

                // 执行错误回调
                if (config.onError) {
                    config.onError(result);
                }
            }

            return result;
        })
        .catch(error => {
            console.error('AJAX Error:', error);

            // 恢复按钮状态
            if (config.showLoading && button) {
                button.disabled = false;
                button.innerHTML = button.dataset.originalText || button.innerHTML;
            }

            // 显示错误消息
            ToastManager.error(error.message || '网络请求失败,请重试');

            // 执行错误回调
            if (config.onError) {
                config.onError(error);
            }

            throw error;
        });
    },

    /**
     * 绑定表单自动提交 (用于页面级别的表单绑定)
     * @param {string} selector - 表单选择器
     * @param {Object} options - 配置选项
     */
    bind(selector, options = {}) {
        document.addEventListener('submit', function(e) {
            const form = e.target;
            if (form.matches(selector)) {
                e.preventDefault();
                AjaxForm.submit(form, options);
            }
        });
    },

    /**
     * 绑定单个表单
     * @param {HTMLFormElement} form - 表单元素
     * @param {Object} options - 配置选项
     */
    bindOne(form, options = {}) {
        form.addEventListener('submit', function(e) {
            e.preventDefault();
            AjaxForm.submit(form, options);
        });
    }
};

// 确认提示辅助函数
function confirmAction(message, callback) {
    if (confirm(message)) {
        callback();
    }
}

// 修复表格内下拉菜单被遮挡问题
const DropdownFixer = {
    init() {
        // 使用 MutationObserver 监听下拉菜单的 show 类
        const observer = new MutationObserver(function(mutations) {
            mutations.forEach(function(mutation) {
                if (mutation.type === 'attributes' && mutation.attributeName === 'class') {
                    const menu = mutation.target;
                    if (menu.classList.contains('dropdown-menu') && menu.closest('.table')) {
                        if (menu.classList.contains('show')) {
                            // 菜单打开时，修复定位
                            const btnGroup = menu.closest('.btn-group');
                            if (btnGroup) {
                                const rect = btnGroup.getBoundingClientRect();
                                menu.style.position = 'fixed';
                                menu.style.zIndex = '9999';
                                menu.style.top = (rect.bottom + 2) + 'px';
                                menu.style.left = 'auto';
                                menu.style.right = (window.innerWidth - rect.right) + 'px';
                                menu.style.transform = 'none';
                                menu.style.inset = 'auto';
                            }
                        } else {
                            // 菜单关闭时，重置样式
                            menu.style.position = '';
                            menu.style.zIndex = '';
                            menu.style.top = '';
                            menu.style.left = '';
                            menu.style.right = '';
                            menu.style.transform = '';
                            menu.style.inset = '';
                        }
                    }
                }
            });
        });

        // 监听整个文档的属性变化
        observer.observe(document.body, {
            attributes: true,
            subtree: true,
            attributeFilter: ['class']
        });
    }
};

// 页面加载完成后自动初始化
document.addEventListener('DOMContentLoaded', function() {
    ToastManager.init();
    DropdownFixer.init();
});
