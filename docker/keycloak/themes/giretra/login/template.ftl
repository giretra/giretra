<#macro registrationLayout bodyClass="" displayInfo=false displayMessage=true displayRequiredFields=false>
<!DOCTYPE html>
<html class="${properties.kcHtmlClass!}" lang="${lang!'en'}">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="robots" content="noindex, nofollow">

    <title>${msg("loginTitle",(realm.displayName!''))}</title>

    <link rel="icon" href="${url.resourcesPath}/img/favicon.ico" type="image/x-icon">
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">

    <#if properties.stylesCommon?has_content>
        <#list properties.stylesCommon?split(' ') as style>
            <link href="${url.resourcesCommonPath}/${style}" rel="stylesheet" />
        </#list>
    </#if>
    <#if properties.styles?has_content>
        <#list properties.styles?split(' ') as style>
            <link href="${url.resourcesPath}/${style}" rel="stylesheet" />
        </#list>
    </#if>

    <#if properties.scripts?has_content>
        <#list properties.scripts?split(' ') as script>
            <script src="${url.resourcesPath}/${script}" type="text/javascript"></script>
        </#list>
    </#if>
</head>

<body class="${properties.kcBodyClass!}" data-page-id="${pageId!''}">
    <#if (pageId!'') == "login.ftl">
        <div class="${properties.kcLoginClass!}">
            <div class="giretra-two-col">
                <div class="giretra-auth-col">
                    <p class="giretra-tagline">A digital playground for Malagasy-style Belote</p>
                    <div class="${properties.kcFormCardClass!}">
                        <header class="${properties.kcFormHeaderClass!}">
                            <#if !(auth?has_content && auth.showUsername?? && auth.showUsername() && auth.showResetCredentials?? && !auth.showResetCredentials())>
                                <#nested "header">
                            <#else>
                                <div class="giretra-attempted-user">
                                    <span>${kcSanitize(auth.attemptedUsername)?no_esc}</span>
                                    <a href="${url.loginRestartFlowUrl}" class="giretra-restart-link"
                                       aria-label="${msg("restartLoginTooltip")}">
                                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                                             stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/>
                                            <path d="M3 3v5h5"/>
                                        </svg>
                                    </a>
                                </div>
                            </#if>
                        </header>

                        <div id="kc-content">
                            <div id="kc-content-wrapper">
                                <#if displayMessage && message?has_content && (message.type != 'warning' || !isAppInitiatedAction??)>
                                    <div class="giretra-alert giretra-alert-${message.type}">
                                        <span class="giretra-alert-text">${kcSanitize(message.summary)?no_esc}</span>
                                    </div>
                                </#if>

                                <#nested "form">

                                <#if auth?has_content && auth.showTryAnotherWay?? && auth.showTryAnotherWay()>
                                    <form id="kc-select-try-another-way-form" action="${url.loginAction}" method="post">
                                        <input type="hidden" name="tryAnotherWay" value="on"/>
                                        <a href="#" id="try-another-way"
                                           onclick="document.getElementById('kc-select-try-another-way-form').submit();return false;">
                                            ${msg("doTryAnotherWay")}
                                        </a>
                                    </form>
                                </#if>

                                <#nested "socialProviders">

                                <#if displayInfo>
                                    <div class="giretra-info">
                                        <#nested "info">
                                    </div>
                                </#if>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="giretra-engage">
                    <div class="giretra-engage-content">
                        <div class="giretra-engage-card">
                            <h3 class="giretra-engage-heading">Learn the rules & build a bot</h3>
                            <p class="giretra-engage-text">Discover Belote Gasy, master the strategy, and create your own AI player to compete.</p>
                            <a href="https://giretra.com" target="_blank" rel="noopener noreferrer" class="giretra-engage-btn-primary">Get started</a>
                        </div>

                        <div class="giretra-engage-card">
                            <h3 class="giretra-engage-heading">Contribute</h3>
                            <p class="giretra-engage-text">Giretra is open source. Help improve the game engine, add features, or fix bugs.</p>
                            <a href="https://github.com/haga-rak/giretra" target="_blank" rel="noopener noreferrer" class="giretra-engage-btn-outline">
                                <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" style="vertical-align: middle; margin-right: 0.375rem;">
                                    <path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"/>
                                </svg>
                                View on GitHub
                            </a>
                        </div>

                        <div class="giretra-engage-screenshot">
                            <img src="${url.resourcesPath}/img/belotegasy.png" alt="Belote Gasy gameplay" class="giretra-screenshot-img">
                        </div>
                    </div>
                </div>
            </div>

            <footer class="giretra-footer">
                <p>&copy; ${.now?string('yyyy')} Giretra</p>
            </footer>
        </div>
    <#else>
        <div class="${properties.kcLoginClass!}">

            <div class="giretra-brand">
                <img src="${url.resourcesPath}/img/giretra-banner.png" alt="Giretra" class="giretra-banner">
            </div>

            <div class="${properties.kcFormCardClass!}">
                <header class="${properties.kcFormHeaderClass!}">
                    <#if !(auth?has_content && auth.showUsername?? && auth.showUsername() && auth.showResetCredentials?? && !auth.showResetCredentials())>
                        <#nested "header">
                    <#else>
                        <div class="giretra-attempted-user">
                            <span>${kcSanitize(auth.attemptedUsername)?no_esc}</span>
                            <a href="${url.loginRestartFlowUrl}" class="giretra-restart-link"
                               aria-label="${msg("restartLoginTooltip")}">
                                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                                     stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/>
                                    <path d="M3 3v5h5"/>
                                </svg>
                            </a>
                        </div>
                    </#if>
                </header>

                <div id="kc-content">
                    <div id="kc-content-wrapper">
                        <#if displayMessage && message?has_content && (message.type != 'warning' || !isAppInitiatedAction??)>
                            <div class="giretra-alert giretra-alert-${message.type}">
                                <span class="giretra-alert-text">${kcSanitize(message.summary)?no_esc}</span>
                            </div>
                        </#if>

                        <#nested "form">

                        <#if auth?has_content && auth.showTryAnotherWay?? && auth.showTryAnotherWay()>
                            <form id="kc-select-try-another-way-form" action="${url.loginAction}" method="post">
                                <input type="hidden" name="tryAnotherWay" value="on"/>
                                <a href="#" id="try-another-way"
                                   onclick="document.getElementById('kc-select-try-another-way-form').submit();return false;">
                                    ${msg("doTryAnotherWay")}
                                </a>
                            </form>
                        </#if>

                        <#nested "socialProviders">

                        <#if displayInfo>
                            <div class="giretra-info">
                                <#nested "info">
                            </div>
                        </#if>
                    </div>
                </div>
            </div>

            <footer class="giretra-footer">
                <p>&copy; ${.now?string('yyyy')} Giretra</p>
            </footer>
        </div>
    </#if>
</body>
</html>
</#macro>
