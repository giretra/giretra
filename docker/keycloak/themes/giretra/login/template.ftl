<#macro registrationLayout bodyClass="" displayInfo=false displayMessage=true displayRequiredFields=false>
<!DOCTYPE html>
<html class="${properties.kcHtmlClass!}" lang="${lang}">
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
    <div class="${properties.kcLoginClass!}">

        <div class="giretra-brand">
            <div class="giretra-suits">
                <span class="suit-spade">&#9824;</span>
                <span class="suit-heart">&#9829;</span>
                <span class="suit-diamond">&#9830;</span>
                <span class="suit-club">&#9827;</span>
            </div>
            <h1 class="giretra-logo">GIRETRA</h1>
            <p class="giretra-tagline">Belote Malagasy</p>
        </div>

        <div class="${properties.kcFormCardClass!}">
            <header class="${properties.kcFormHeaderClass!}">
                <#if !(auth?has_content && auth.showUsername() && !auth.showResetCredentials())>
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

                    <#if auth?has_content && auth.showTryAnotherWay()>
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
</body>
</html>
</#macro>
