/**
 * 侧边栏响应式控制器
 * 用于处理侧边栏的展开/收起状态和响应式行为
 */

(function() {
    'use strict';

    const SidebarController = {
        // 配置
        config: {
            storageKey: 'zircon-sidebar-state',
            collapseBreakpoint: 1366, // 小于此宽度自动收起
            transitionDuration: 300 // 与 CSS 中的 --sidebar-transition 匹配
        },

        // DOM 元素引用
        elements: {
            sidebar: null,
            mainContent: null,
            toggleBtn: null
        },

        // 当前状态
        state: {
            isCollapsed: false,
            isForceExpanded: false // 用户强制展开状态
        },

        /**
         * 初始化侧边栏控制器
         */
        init() {
            // 获取 DOM 元素
            this.elements.sidebar = document.querySelector('.sidebar');
            this.elements.mainContent = document.querySelector('.main-content');

            // 如果侧边栏不存在（登录页面等），直接返回
            if (!this.elements.sidebar) return;

            // 创建切换按钮
            this.createToggleButton();

            // 加载保存的状态
            this.loadState();

            // 检查当前屏幕宽度并自动调整
            this.checkScreenWidth();

            // 绑定事件
            this.bindEvents();

            // 应用初始状态
            this.applyState();
        },

        /**
         * 创建侧边栏切换按钮
         */
        createToggleButton() {
            const toggleBtn = document.createElement('div');
            toggleBtn.className = 'sidebar-toggle-btn';
            toggleBtn.innerHTML = '<i class="bi bi-chevron-left"></i>';
            toggleBtn.setAttribute('title', '切换侧边栏');
            toggleBtn.setAttribute('role', 'button');
            toggleBtn.setAttribute('aria-label', '切换侧边栏');

            this.elements.sidebar.appendChild(toggleBtn);
            this.elements.toggleBtn = toggleBtn;
        },

        /**
         * 绑定事件监听器
         */
        bindEvents() {
            // 切换按钮点击事件
            if (this.elements.toggleBtn) {
                this.elements.toggleBtn.addEventListener('click', () => {
                    this.toggle();
                });
            }

            // 窗口大小改变事件
            let resizeTimer;
            window.addEventListener('resize', () => {
                // 防抖处理
                clearTimeout(resizeTimer);
                resizeTimer = setTimeout(() => {
                    this.checkScreenWidth();
                }, 150);
            });

            // 键盘快捷键 (Ctrl/Cmd + B)
            document.addEventListener('keydown', (e) => {
                if ((e.ctrlKey || e.metaKey) && e.key === 'b') {
                    e.preventDefault();
                    this.toggle();
                }
            });
        },

        /**
         * 切换侧边栏状态
         * @param {boolean} forceState - 强制设置状态（可选）
         */
        toggle(forceState) {
            const newState = forceState !== undefined ? forceState : !this.state.isCollapsed;

            // 如果尝试在小屏幕上展开，设置为强制展开模式
            if (newState === false && window.innerWidth < this.config.collapseBreakpoint) {
                this.state.isForceExpanded = true;
                this.elements.sidebar.classList.add('force-expanded');
            } else {
                this.state.isForceExpanded = false;
                this.elements.sidebar.classList.remove('force-expanded');
            }

            this.state.isCollapsed = newState;
            this.applyState();
            this.saveState();
        },

        /**
         * 展开侧边栏
         */
        expand() {
            this.toggle(false);
        },

        /**
         * 收起侧边栏
         */
        collapse() {
            this.toggle(true);
        },

        /**
         * 检查屏幕宽度并自动调整
         */
        checkScreenWidth() {
            const screenWidth = window.innerWidth;

            if (screenWidth < this.config.collapseBreakpoint) {
                // 小屏幕：自动收起（除非用户强制展开）
                if (!this.state.isForceExpanded) {
                    this.state.isCollapsed = true;
                    this.applyState();
                }
            } else {
                // 大屏幕：自动展开（如果之前没有被手动收起）
                const savedState = this.loadState();
                if (savedState === null) {
                    // 没有保存的状态，默认展开
                    this.state.isCollapsed = false;
                    this.state.isForceExpanded = false;
                    this.applyState();
                }
            }
        },

        /**
         * 应用当前状态到 DOM
         */
        applyState() {
            if (this.state.isCollapsed) {
                this.elements.sidebar.classList.add('collapsed');
                this.elements.mainContent.classList.add('expanded');
            } else {
                this.elements.sidebar.classList.remove('collapsed');
                this.elements.mainContent.classList.remove('expanded');
            }

            // 更新切换按钮图标
            const icon = this.elements.toggleBtn?.querySelector('i');
            if (icon) {
                icon.className = this.state.isCollapsed ? 'bi bi-chevron-right' : 'bi bi-chevron-left';
            }

            // 触发自定义事件
            this.dispatchEvent();
        },

        /**
         * 保存状态到 localStorage
         */
        saveState() {
            try {
                localStorage.setItem(this.config.storageKey, JSON.stringify({
                    isCollapsed: this.state.isCollapsed,
                    timestamp: Date.now()
                }));
            } catch (e) {
                console.warn('无法保存侧边栏状态:', e);
            }
        },

        /**
         * 从 localStorage 加载状态
         * @returns {boolean|null} 返回保存的状态，如果没有则返回 null
         */
        loadState() {
            try {
                const saved = localStorage.getItem(this.config.storageKey);
                if (saved) {
                    const data = JSON.parse(saved);
                    // 只在 24 小时内有效的状态
                    if (Date.now() - data.timestamp < 24 * 60 * 60 * 1000) {
                        this.state.isCollapsed = data.isCollapsed;
                        return data.isCollapsed;
                    }
                }
            } catch (e) {
                console.warn('无法加载侧边栏状态:', e);
            }
            return null;
        },

        /**
         * 触发侧边栏状态变化事件
         */
        dispatchEvent() {
            const event = new CustomEvent('sidebarToggle', {
                detail: {
                    isCollapsed: this.state.isCollapsed
                }
            });
            window.dispatchEvent(event);
        },

        /**
         * 重置为默认状态
         */
        reset() {
            try {
                localStorage.removeItem(this.config.storageKey);
            } catch (e) {
                console.warn('无法重置侧边栏状态:', e);
            }
            this.state.isCollapsed = false;
            this.state.isForceExpanded = false;
            this.applyState();
        }
    };

    // 将控制器暴露到全局
    window.SidebarController = SidebarController;

    // 页面加载完成后初始化
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            SidebarController.init();
        });
    } else {
        SidebarController.init();
    }

    // 导出供其他模块使用
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = SidebarController;
    }

})();
