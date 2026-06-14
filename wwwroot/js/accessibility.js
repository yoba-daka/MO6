"use strict";
var _typeof = "function" == typeof Symbol && "symbol" == typeof Symbol.iterator ? function (n) {
    return typeof n
} : function (n) {
    return n && "function" == typeof Symbol && n.constructor === Symbol && n !== Symbol.prototype ? "symbol" : typeof n
};
! function (n) {
    function e(n) {
        return n = n || "", u.filter(function (e) {
            return n.match(new RegExp(e + "$", "gi"))
        }).pop()
    }

    function t() {
        var n = window.chrome,
            e = window.navigator,
            t = e.vendor,
            i = e.userAgent.indexOf("OPR") > -1,
            o = e.userAgent.indexOf("Edge") > -1;
        return null !== n && void 0 !== n && "Google Inc." === t && 0 == i && 0 == o
    }

    function i() {
        var n = navigator.userAgent || navigator.vendor || window.opera,
            e = n.substr(0, 4);
        return /(android|bb\d+|meego).+mobile|avantgo|bada\/|blackberry|blazer|compal|elaine|fennec|hiptop|iemobile|ip(hone|od)|iris|kindle|lge |maemo|midp|mmp|mobile.+firefox|netfront|opera m(ob|in)i|palm( os)?|phone|p(ixi|re)\/|plucker|pocket|psp|series(4|6)0|symbian|treo|up\.(browser|link)|vodafone|wap|windows ce|xda|xiino/i.test(n) || /1207|6310|6590|3gso|4thp|50[1-6]i|770s|802s|a wa|abac|ac(er|oo|s\-)|ai(ko|rn)|al(av|ca|co)|amoi|an(ex|ny|yw)|aptu|ar(ch|go)|as(te|us)|attw|au(di|\-m|r |s )|avan|be(ck|ll|nq)|bi(lb|rd)|bl(ac|az)|br(e|v)w|bumb|bw\-(n|u)|c55\/|capi|ccwa|cdm\-|cell|chtm|cldc|cmd\-|co(mp|nd)|craw|da(it|ll|ng)|dbte|dc\-s|devi|dica|dmob|do(c|p)o|ds(12|\-d)|el(49|ai)|em(l2|ul)|er(ic|k0)|esl8|ez([4-7]0|os|wa|ze)|fetc|fly(\-|_)|g1 u|g560|gene|gf\-5|g\-mo|go(\.w|od)|gr(ad|un)|haie|hcit|hd\-(m|p|t)|hei\-|hi(pt|ta)|hp( i|ip)|hs\-c|ht(c(\-| |_|a|g|p|s|t)|tp)|hu(aw|tc)|i\-(20|go|ma)|i230|iac( |\-|\/)|ibro|idea|ig01|ikom|im1k|inno|ipaq|iris|ja(t|v)a|jbro|jemu|jigs|kddi|keji|kgt( |\/)|klon|kpt |kwc\-|kyo(c|k)|le(no|xi)|lg( g|\/(k|l|u)|50|54|\-[a-w])|libw|lynx|m1\-w|m3ga|m50\/|ma(te|ui|xo)|mc(01|21|ca)|m\-cr|me(rc|ri)|mi(o8|oa|ts)|mmef|mo(01|02|bi|de|do|t(\-| |o|v)|zz)|mt(50|p1|v )|mwbp|mywa|n10[0-2]|n20[2-3]|n30(0|2)|n50(0|2|5)|n7(0(0|1)|10)|ne((c|m)\-|on|tf|wf|wg|wt)|nok(6|i)|nzph|o2im|op(ti|wv)|oran|owg1|p800|pan(a|d|t)|pdxg|pg(13|\-([1-8]|c))|phil|pire|pl(ay|uc)|pn\-2|po(ck|rt|se)|prox|psio|pt\-g|qa\-a|qc(07|12|21|32|60|\-[2-7]|i\-)|qtek|r380|r600|raks|rim9|ro(ve|zo)|s55\/|sa(ge|ma|mm|ms|ny|va)|sc(01|h\-|oo|p\-)|sdk\/|se(c(\-|0|1)|47|mc|nd|ri)|sgh\-|shar|sie(\-|m)|sk\-0|sl(45|id)|sm(al|ar|b3|it|t5)|so(ft|ny)|sp(01|h\-|v\-|v )|sy(01|mb)|t2(18|50)|t6(00|10|18)|ta(gt|lk)|tcl\-|tdg\-|tel(i|m)|tim\-|t\-mo|to(pl|sh)|ts(70|m\-|m3|m5)|tx\-9|up(\.b|g1|si)|utst|v400|v750|veri|vi(rg|te)|vk(40|5[0-3]|\-v)|vm40|voda|vulc|vx(52|53|60|61|70|80|81|83|85|98)|w3c(\-| )|webc|whit|wi(g |nc|nw)|wmlb|wonu|x700|yas\-|your|zeto|zte\-/i.test(e)
    }

    function o() {
        var n;
        try {
            n = localStorage.getItem(m), n = JSON.parse(n)
        } catch (n) { }
        return n && "object" === ("undefined" == typeof n ? "undefined" : _typeof(n)) ? n : {}
    }

    function s(n) {
        localStorage.setItem(m, JSON.stringify(n))
    }

    function a(t, i) {
        n(t).not(".open-accessibility *").each(function () {
            var t = n(this),
                o = t.attr("data-open-accessibility-text-original");
            o || (o = t.css("font-size"), t.attr("data-open-accessibility-text-original", o));
            var s = e(o) || "",
                a = parseFloat(o) * i;
            t.css("font-size", a + s)
        })
    }

    function c(e) {
        var t = n(".open-accessibility-menu");
        Object.keys(e).forEach(function (n, i) {
            t.find('[data-lang="' + n + '"]').text(e[n])
        })
    }

    function l(e, t) {
        var i = {};
        return e.forEach(function (e) {
            var o = t && t[e] || d[e];
            n.isPlainObject(o) ? i[e] = o : console.error(e + "language does not set!")
        }), i
    }

    function r(n) {
        var e = "open-accessibility-size-";
        return e + n
    }
    var d = {
        he: {
            "zoom-out": "הקטן",
            "zoom-in": "הגדל",
            invert: "היפוך צבעים",
            "bigger-mouse": "עכבר גדול",
            brightness: "בהירות",
            contrast: "ניגודיות",
            "highlight-links": "הדגשת קישורים",
            "text-spacing": "מרווח טקסט",
            grayscale: "גווני אפור",
            reset: "ביטול שינויים"
        },
        en: {
            "zoom-out": "Zoom Out",
            "zoom-in": "Zoom In",
            invert: "Invert",
            "bigger-cursor": "Bigger Cursor",
            brightness: "Brightness",
            contrast: "Contrast",
            "highlight-links": "Highlight Links",
            "text-spacing": "Text Spacing",
            grayscale: "Grayscale",
            reset: "Undo Changes"
        }
    },
        p = '<div class="open-accessibility-cursor-workaround"></div>\n<div class="open-accessibility open-accessibility-collapsed">\n    <div class="open-accessibility-container">\n\n        <div class="open-accessibility-expand-button" role="button" tabindex="0" aria-label="פתיחת תפריט נגישות">\n            <?xml version="1.0" encoding="utf-8"?>\n<svg viewBox="0 2 24 24" xmlns="http://www.w3.org/2000/svg" aria-hidden="true" focusable="false">\n    <path d="M0 0h24v24H0z" fill="none"/>\n    <circle cx="12" cy="4" r="2"/>\n    <path d="M19 13v-2c-1.54.02-3.09-.75-4.07-1.83l-1.29-1.43c-.17-.19-.38-.34-.61-.45-.01 0-.01-.01-.02-.01H13c-.35-.2-.75-.3-1.19-.26C10.76 7.11 10 8.04 10 9.09V15c0 1.1.9 2 2 2h5v5h2v-5.5c0-1.1-.9-2-2-2h-3v-3.45c1.29 1.07 3.25 1.94 5 1.95zm-6.17 5c-.41 1.16-1.52 2-2.83 2-1.66 0-3-1.34-3-3 0-1.31.84-2.41 2-2.83V12.1c-2.28.46-4 2.48-4 4.9 0 2.76 2.24 5 5 5 2.42 0 4.44-1.72 4.9-4h-2.07z"/>\n</svg>\n        </div>\n\n        <div class="open-accessibility-menu">\n            <div class="open-accessibility-close-button" role="button" tabindex="0" aria-label="סגירת תפריט נגישות">\n                <?xml version="1.0" encoding="utf-8"?>\n<svg version="1.0" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" x="0px" y="0px"\n    viewBox="0 0 40 40" enable-background="new 0 0 40 40" xml:space="preserve" aria-hidden="true" focusable="false">\n<path fill="#000000" d="M22,20l5.9,5.9l-2,2L20,22l-5.9,5.9l-2-2L18,20l-5.9-5.9l2-2L20,18l5.9-5.9l2,2L22,20z"/>\n</svg>\n                \n            </div>\n\n            <div class="open-accessibility-menu-button open-accessibility-zoom-out-button" role="button" tabindex="0">\n                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 151 106" aria-hidden="true" focusable="false"><title>zoom-out</title><g id="Exploded_Icons" data-name="Exploded Icons"><path id="_Compound_Path_" data-name="&lt;Compound Path&gt;" d="M83.14,50.55H81.38l-.62-.6a14.44,14.44,0,1,0-1.56,1.56l.6.62v1.75L90.91,65l3.31-3.31Zm-13.34,0a10,10,0,1,1,10-10A10,10,0,0,1,69.8,50.55Zm1.11-11.11H64.25v2.22H75.36V39.44Z"/></g></svg>\n                <span data-lang="zoom-out">הקטן</span>\n            </div>\n\n            <div class="open-accessibility-menu-button open-accessibility-zoom-in-button" role="button" tabindex="0">\n                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 151 106" aria-hidden="true" focusable="false"><title>zoom-in</title><g id="Exploded_Icons" data-name="Exploded Icons"><path id="_Compound_Path_" data-name="&lt;Compound Path&gt;" d="M84.53,50.55H82.77l-.62-.6a14.33,14.33,0,1,0-1.55,1.56l.6.62v1.75L92.31,65l3.31-3.31Zm-13.33,0a10,10,0,1,1,10-10A10,10,0,0,1,71.2,50.55Zm5.55-8.89H72.31v4.45H70.08V41.66H65.64V39.44h4.44V35h2.23v4.45h4.44Z"/></g></svg>\n                <span data-lang="zoom-in">הגדל</span>\n            </div>\n\n            <div class="open-accessibility-menu-button open-accessibility-invert-button" role="button" tabindex="0">\n                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 151 106" aria-hidden="true" focusable="false"><title>invert</title><g id="Exploded_Icons" data-name="Exploded Icons"><path id="_Compound_Path_" data-name="&lt;Compound Path&gt;" d="M86.65,37.45,75.54,26.34,64.42,37.45a15.71,15.71,0,1,0,22.23,0ZM75.54,60.35a11.78,11.78,0,0,1-8.33-20.11l8.33-8.35Z"/></g></svg>                \n                <span data-lang="invert">היפוך צבעים</span>\n            </div>\n\n            <div class="open-accessibility-menu-button open-accessibility-cursor-button" role="button" tabindex="0">\n                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 151 106" aria-hidden="true" focusable="false"><title>cursor</title><g id="Exploded_Icons" data-name="Exploded Icons"><path id="_Compound_Path_" data-name="&lt;Compound Path&gt;" d="M83,47.53l-.07-.07V46H81.52L81.45,46V44.55H80L80,44.48V43.06H78.55L78.48,43V41.58H77.06L77,41.51V40.09H75.57L75.5,40V38.6H74.08L74,38.53V37.11H72.59L72.52,37V35.62H71.11L71,35.55V34.14H69.62l-.07-.07V32.65H68.13l-.07-.07V31.16H66.57V56.45h3V55l.07-.07H71V53.55l.07-.07h1.41V52.06l.07-.07h1.35l.07.07V55h1.42l.07.07v2.9h1.42L77,58v1.42h3V58l.07-.07h1.42V55H80L80,54.9V52H78.55l-.07-.07V50.5h6v-3ZM78.48,49H77v3H78.4l.08.07V55h1.41L80,55v2.83l-.07.07H77.06L77,57.87V55H75.57l-.07-.07V52H74.08L74,51.92V50.5H72.52v1.42l-.07.07H71v1.42l-.07.07H69.55V54.9l-.07.07H68.06V34.14h1.42l.07.07v1.41H71l.07.07v1.42h1.41l.07.07V38.6h1.42l.07.07v1.42h1.42l.07.07v1.42h1.42l.07.07v1.41H78.4l.08.07v1.42h1.41l.07.07V46h1.42l.07.07v1.42h1.42l.07.07V49Z"/></g></svg>\n                <span data-lang="bigger-cursor">עכבר גדול</span>\n            </div>\n\n            <div class="open-accessibility-menu-button open-accessibility-brightness-button" role="button" tabindex="0">\n                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 151 106" aria-hidden="true" focusable="false"><title>brightness</title><g id="Exploded_Icons" data-name="Exploded Icons"><path id="_Compound_Path_" data-name="&lt;Compound Path&gt;" d="M88.29,50.51l5.22-5.21-5.22-5.22v-7.4H80.9l-5.22-5.21-5.22,5.21H63.07v7.4L57.85,45.3l5.22,5.21v7.4h7.39l5.22,5.22,5.22-5.22h7.39ZM75.68,54.75V35.84a9.46,9.46,0,1,1,0,18.91Z"/></g></svg>                \n                <span data-lang="brightness">בהירות</span>\n            </div>\n\n            <div class="open-accessibility-menu-button open-accessibility-contrast-button" role="button" tabindex="0">\n                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 151 106" aria-hidden="true" focusable="false"><title>contrast</title><g id="Exploded_Icons" data-name="Exploded Icons"><path id="_Compound_Path_" data-name="&lt;Compound Path&gt;" d="M71.85,28.87H62.73a3.66,3.66,0,0,0-3.65,3.65V58.07a3.66,3.66,0,0,0,3.65,3.65h9.12v3.65H75.5V25.22H71.85Zm0,27.37H62.73L71.85,45.3ZM88.27,28.87H79.15v3.65h9.12V56.24L79.15,45.3V61.72h9.12a3.66,3.66,0,0,0,3.65-3.65V32.52A3.66,3.66,0,0,0,88.27,28.87Z"/></g></svg>\n                <span data-lang="contrast">ניגודיות</span>\n            </div>\n\n            <div class="open-accessibility-menu-button open-accessibility-monochrome-button" role="button" tabindex="0">\n                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 151 106" aria-hidden="true" focusable="false"><title>monochrome</title><g id="Exploded_Icons" data-name="Exploded Icons"><path id="_Compound_Path_" data-name="&lt;Compound Path&gt;" d="M90.28,32.87H84.44l-3.28-3.65h-11l-3.29,3.65H61.08a3.66,3.66,0,0,0-3.65,3.65V58.43a3.66,3.66,0,0,0,3.65,3.65h29.2a3.66,3.66,0,0,0,3.65-3.65V36.52A3.66,3.66,0,0,0,90.28,32.87Zm0,25.56H75.68V56.6a9.13,9.13,0,0,1,0-18.25V36.52h14.6Zm-5.47-11a9,9,0,0,0-9.13-9.12v3.28a5.84,5.84,0,0,1,0,11.68V56.6A9,9,0,0,0,84.81,47.47Zm-15,0a5.77,5.77,0,0,0,5.84,5.84V41.63A5.77,5.77,0,0,0,69.84,47.47Z"/></g></svg>\n                <span data-lang="grayscale">גווני אפור</span>\n            </div>\n\n            <div class="open-accessibility-menu-button open-accessibility-reset-button" role="button" tabindex="0">\n                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 151 106" aria-hidden="true" focusable="false"><title>revert</title><g id="Exploded_Icons" data-name="Exploded Icons"><path id="_Compound_Path_" data-name="&lt;Compound Path&gt;" d="M75.26,38.68A17,17,0,0,0,64,42.91l-5.86-5.86V51.7H72.81l-5.89-5.89A13,13,0,0,1,87.63,51.7l3.86-1.27A17.12,17.12,0,0,0,75.26,38.68Z"/></g></svg>\n                <span data-lang="reset">בטל שינויים</span>\n            </div>\n\n\n            <div class="open-accessibility-menu-footer">\n   <a href="/הצהרת-נגישות">הצהרת נגישות</a>\n            </div>\n\n\n        </div>\n\n\n    </div>\n</div>',
        m = "open-accessibility-config",
        u = ["px", "cm", "em", "ex", "in", "mm", "pc", "pt", "vh", "vw", "vmin"];
    var initial = false;
    n.fn.openAccessibility = function (e) {
        function d() {
            b.isMenuOpened ? (x.fadeOut(300), f.fadeIn(300), w.removeClass("open-accessibility-collapsed"), w.addClass("open-accessibility-expanded")) : (x.fadeIn(300), f.fadeOut(300), w.removeClass("open-accessibility-expanded"), w.addClass("open-accessibility-collapsed"));
            var n = [];
            b.invert && n.push("invert(1)"), n.push("brightness(" + b.brightness + "%)"), n.push("grayscale(" + b.grayscale + "%)");
            var e = n.join(" ");
            if (initial) {
                y.css("filter", e), y.css("-ms-filter", e), y.css("-moz-filter", e), y.css("-webkit-filter", e), y.css("-o-filter", e)
            }
            h.toggleClass("open-accessibility-highlighted-links", !!b.highlightedLinks);
            h.toggleClass("open-accessibility-text-spacing", !!b.textSpacing);
            h.toggleClass("open-accessibility-high-contrast", b.contrast > 100);
            L.attr("aria-pressed", b.contrast > 100 ? "true" : "false");
            R.attr("aria-pressed", b.highlightedLinks ? "true" : "false");
            T.attr("aria-pressed", b.textSpacing ? "true" : "false");
            a(b.textSelector, b.zoom), b.cursor ? (h.addClass("open-accessibility-cursor"), O || I.show()) : (h.removeClass("open-accessibility-cursor"), O || I.hide()), s(b)
        }
        var m = this;
        e = e || {};
        var u = {
            isMenuOpened: !1,
            highlightedLinks: !1,
            textSpacing: !1,
            isMobileEnabled: !0,
            grayscale: 0,
            brightness: 100,
            contrast: 100,
            maxZoomLevel: 3,
            minZoomLevel: .5,
            zoomStep: .2,
            zoom: 1,
            cursor: !1,
            textSelector: ".open-accessibility-text",
            invert: !1,
            localization: ["he"],
            iconSize: "m"
        },
            v = o(),
            g = n.extend({}, u, e),
            b = n.extend({}, g, v, e);
        if (!b.isMobileEnabled && i()) return void console.log("disabling accessibility plugin due to mobile browser");
        m.prepend(p);
        n(".open-accessibility-contrast-button").after('<div class="open-accessibility-menu-button open-accessibility-highlighted-links-button" role="button" tabindex="0">\n                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 151 106" aria-hidden="true" focusable="false"><title>links</title><g><path d="M50 33h51v5H50zm0 17h51v5H50zm0 17h51v5H50zm18 9h15v4H68z"/></g></svg>\n                <span data-lang="highlight-links">הדגשת קישורים</span>\n            </div>');
        n(".open-accessibility-highlighted-links-button").after('<div class="open-accessibility-menu-button open-accessibility-text-spacing-button" role="button" tabindex="0">\n                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 151 106" aria-hidden="true" focusable="false"><title>text-spacing</title><g><path d="M45 28h61v6H45zm0 22h61v6H45zm0 22h61v6H45zM31 31l8-8 8 8h-6v45h6l-8 8-8-8h6V31z"/></g></svg>\n                <span data-lang="text-spacing">מרווח טקסט</span>\n            </div>');
        var h = n("html"),
            y = n(".bodyAccessibility"),
            w = n(".open-accessibility"),
            f = n(".open-accessibility-menu"),
            x = n(".open-accessibility-expand-button"),
            k = n(".open-accessibility-close-button"),
            V = n(".open-accessibility-invert-button"),
            z = n(".open-accessibility-cursor-button"),
            H = n(".open-accessibility-zoom-in-button"),
            _ = n(".open-accessibility-zoom-out-button"),
            M = n(".open-accessibility-brightness-button"),
            C = n(".open-accessibility-monochrome-button"),
            L = n(".open-accessibility-contrast-button"),
            R = n(".open-accessibility-highlighted-links-button"),
            T = n(".open-accessibility-text-spacing-button"),
            Z = n(".open-accessibility-reset-button"),
            I = n(".open-accessibility-cursor-workaround");
        w.addClass(r(b.iconSize));
        var E = l(b.localization, b.localizationMap);
        c(E[Object.keys(E)[0]]);
        var A = x.add(k).add(V).add(z).add(H).add(_).add(M).add(C).add(L).add(R).add(T).add(Z);
        A.attr({
            role: "button",
            tabindex: "0"
        });
        x.attr("aria-label", "פתיחת תפריט נגישות");
        k.attr("aria-label", "סגירת תפריט נגישות");
        n(".open-accessibility-menu-button").each(function () {
            var e = n(this).find("span").text().trim();
            e && n(this).attr("aria-label", e);
        });
        A.find("svg").attr({
            "aria-hidden": "true",
            focusable: "false"
        });
        A.on("keydown", function (e) {
            if (e.key === "Enter" || e.key === " " || e.which === 13 || e.which === 32) {
                e.preventDefault();
                n(this).trigger("click");
            }
        });
        h.addClass("open-accessibility-zoom"), M.click(function () {
            initial = true;
            b.brightness += 50, b.brightness > 150 && (b.brightness = 50), d()
        }), L.click(function () {
            initial = true;
            b.contrast = b.contrast > 100 ? 100 : 150, d()
        }), R.click(function () {
            b.highlightedLinks = !b.highlightedLinks, d()
        }), T.click(function () {
            b.textSpacing = !b.textSpacing, d()
        }), C.click(function () {
            initial = true;
            b.grayscale += 100, b.grayscale > 100 && (b.grayscale = 0), d()
        }), Z.click(function () {
            b = n.extend({}, g), b.isMenuOpened = !1, d()
        }), H.click(function () {
            b.zoom = Math.min(b.maxZoomLevel, b.zoom + b.zoomStep), d()
        }), _.click(function () {
            b.zoom = Math.max(b.minZoomLevel, b.zoom - b.zoomStep), d()
        }), V.click(function () {
            initial = true;
            b.invert = !b.invert, d()
        }), z.click(function () {
            b.cursor = !b.cursor, d()
        }), x.click(function () {
            b.isMenuOpened = !0, d()
        }), k.click(function () {
            b.isMenuOpened = !1, d()
        }), n(document).click(function (e) {

            n(e.target).closest(".open-accessibility").length || b.isMenuOpened && (b.isMenuOpened = !1, d())
        }), x.hide(), f.hide(), e.isMenuOpened ? (b.isMenuOpened = !0, f.show(), x.hide()) : b.isMenuOpened = !1, I.hide();
        var O = t();
        O || n(document).on("mousemove", function (n) {
            b.cursor && I.css({
                left: n.pageX / b.zoom,
                top: n.pageY / b.zoom
            })
        }), a(b.textSelector, 1), d()
    }
}(jQuery || $);

(function () {
    "use strict";

    var newWindowText = "(נפתח בלשונית חדשה)";
    var newWindowHintText = "נפתח בלשונית חדשה";
    var activeNewWindowHintTarget = null;
    var newWindowTooltipHideTimer = null;
    var plyrTextMap = {
        "Play": "ניגון",
        "Pause": "השהיה",
        "Mute": "השתקה",
        "Unmute": "ביטול השתקה",
        "Enable captions": "הפעלת כתוביות",
        "Disable captions": "כיבוי כתוביות",
        "Settings": "הגדרות",
        "Captions": "כתוביות",
        "Quality": "איכות",
        "Speed": "מהירות",
        "Normal": "רגיל",
        "Go back to previous menu": "חזרה לתפריט הקודם",
        "Enter fullscreen": "כניסה למסך מלא",
        "Exit fullscreen": "יציאה ממסך מלא",
        "Disabled": "כבוי",
        "% buffered": "% נטען"
    };

    function isHidden(element) {
        return element.hidden || element.offsetParent === null || getComputedStyle(element).display === "none" || getComputedStyle(element).visibility === "hidden";
    }

    function selectAll(root, selector) {
        var scope = root || document;
        var results = [];
        if (scope.nodeType === 1 && scope.matches(selector)) {
            results.push(scope);
        }
        if (scope.querySelectorAll) {
            results = results.concat(Array.prototype.slice.call(scope.querySelectorAll(selector)));
        }
        return results;
    }

    function cleanText(value) {
        return (value || "").replace(/\s+/g, " ").trim();
    }

    function setAttr(element, name, value) {
        if (element.getAttribute(name) !== value) {
            element.setAttribute(name, value);
        }
    }

    function removeAttr(element, name) {
        if (element.hasAttribute(name)) {
            element.removeAttribute(name);
        }
    }

    function installFocusStyles() {
        if (document.getElementById("mo6-focus-visible-overrides")) return;

        var style = document.createElement("style");
        style.id = "mo6-focus-visible-overrides";
        var textFieldFocusSelector = [
            'input:not([type="hidden"]):not([type="checkbox"]):not([type="radio"]):not([type="range"]):not([type="file"]):focus',
            'select:focus',
            'textarea:focus',
            'input:not([type="hidden"]):not([type="checkbox"]):not([type="radio"]):not([type="range"]):not([type="file"]):focus-visible',
            'select:focus-visible',
            'textarea:focus-visible',
            'input:not([type="hidden"]):not([type="checkbox"]):not([type="radio"]):not([type="range"]):not([type="file"]).keyboard-focus-visible',
            'select.keyboard-focus-visible',
            'textarea.keyboard-focus-visible',
            'input:not([type="hidden"]):not([type="checkbox"]):not([type="radio"]):not([type="range"]):not([type="file"])[data-keyboard-focus="true"]',
            'select[data-keyboard-focus="true"]',
            'textarea[data-keyboard-focus="true"]'
        ].join(',');
        style.textContent = [
            'a[href]:focus-visible,button:focus-visible,.btn:focus-visible,[role="button"]:focus-visible,input:not([type="hidden"]):focus-visible,select:focus-visible,textarea:focus-visible,iframe:focus-visible,[tabindex]:not([tabindex="-1"]):focus-visible,.keyboard-focus-visible,[data-keyboard-focus="true"]{outline:2px solid currentColor!important;outline-offset:3px!important;box-shadow:none!important;}',
            textFieldFocusSelector + '{outline:0!important;outline-offset:0!important;border-color:#d2d2d2!important;box-shadow:none!important;}',
            'a[href]:focus-visible,a.keyboard-focus-visible,a[data-keyboard-focus="true"]{text-decoration:underline!important;}',
            '.open-accessibility-expand-button:focus-visible::after,.open-accessibility-expand-button.keyboard-focus-visible::after{content:"";position:absolute;inset:6px;border:2px solid currentColor;border-radius:50%;pointer-events:none;}'
        ].join("\n");
        document.head.appendChild(style);
    }

    function trackKeyboardFocus() {
        if (document.documentElement.hasAttribute("data-mo6-focus-tracking")) return;
        document.documentElement.setAttribute("data-mo6-focus-tracking", "true");

        document.addEventListener("keydown", function (event) {
            if (event.key === "Tab" || event.which === 9) {
                document.documentElement.classList.add("using-keyboard");
            }
        }, true);

        document.addEventListener("mousedown", function () {
            document.documentElement.classList.remove("using-keyboard");
        }, true);

        document.addEventListener("focusin", function (event) {
            var target = event.target;
            if (target && target.nodeType === 1 && document.documentElement.classList.contains("using-keyboard")) {
                target.classList.add("keyboard-focus-visible");
                target.setAttribute("data-keyboard-focus", "true");
            }
        }, true);

        document.addEventListener("focusout", function (event) {
            var target = event.target;
            if (target && target.nodeType === 1) {
                target.classList.remove("keyboard-focus-visible");
                target.removeAttribute("data-keyboard-focus");
            }
        }, true);
    }

    function ensureNewWindowTooltip() {
        var tooltip = document.getElementById("mo6-new-window-tooltip");
        if (tooltip) return tooltip;

        tooltip = document.createElement("div");
        tooltip.id = "mo6-new-window-tooltip";
        tooltip.className = "mo6-new-window-tooltip";
        tooltip.setAttribute("role", "tooltip");
        tooltip.setAttribute("lang", "he");
        tooltip.setAttribute("dir", "rtl");
        tooltip.hidden = true;
        document.body.appendChild(tooltip);
        return tooltip;
    }

    function positionNewWindowTooltip() {
        if (!activeNewWindowHintTarget || !document.body.contains(activeNewWindowHintTarget)) return;

        var tooltip = ensureNewWindowTooltip();
        var targetRect = activeNewWindowHintTarget.getBoundingClientRect();
        var tooltipRect = tooltip.getBoundingClientRect();
        var viewportPadding = 8;
        var viewportLeft = window.pageXOffset + viewportPadding;
        var viewportRight = window.pageXOffset + window.innerWidth - tooltipRect.width - viewportPadding;
        var left = window.pageXOffset + targetRect.left + targetRect.width / 2 - tooltipRect.width / 2;
        var top = window.pageYOffset + targetRect.top - tooltipRect.height - viewportPadding;

        if (top < window.pageYOffset + viewportPadding) {
            top = window.pageYOffset + targetRect.bottom + viewportPadding;
        }

        tooltip.style.left = Math.max(viewportLeft, Math.min(left, viewportRight)) + "px";
        tooltip.style.top = top + "px";
    }

    function showNewWindowTooltip(link) {
        if (!link) return;

        window.clearTimeout(newWindowTooltipHideTimer);
        activeNewWindowHintTarget = link;

        var tooltip = ensureNewWindowTooltip();
        tooltip.textContent = link.getAttribute("data-new-window-hint") || newWindowHintText;
        tooltip.hidden = false;
        tooltip.classList.remove("is-visible");
        tooltip.style.left = "0px";
        tooltip.style.top = "0px";

        positionNewWindowTooltip();
        window.requestAnimationFrame(function () {
            if (activeNewWindowHintTarget === link) {
                tooltip.classList.add("is-visible");
            }
        });
    }

    function hideNewWindowTooltip() {
        var tooltip = document.getElementById("mo6-new-window-tooltip");
        activeNewWindowHintTarget = null;

        if (!tooltip) return;

        tooltip.classList.remove("is-visible");
        window.clearTimeout(newWindowTooltipHideTimer);
        newWindowTooltipHideTimer = window.setTimeout(function () {
            if (!activeNewWindowHintTarget) {
                tooltip.hidden = true;
            }
        }, 150);
    }

    function closestNewWindowHintLink(target) {
        var current = target && target.nodeType === 1 ? target : null;

        while (current) {
            if (current.matches && current.matches('a[target="_blank"][data-new-window-hint]')) {
                return current;
            }
            current = current.parentElement;
        }

        return null;
    }

    function installNewWindowHint() {
        if (document.documentElement.hasAttribute("data-mo6-new-window-hint")) return;
        document.documentElement.setAttribute("data-mo6-new-window-hint", "true");

        document.addEventListener("mouseover", function (event) {
            var link = closestNewWindowHintLink(event.target);
            if (!link || (event.relatedTarget && link.contains(event.relatedTarget))) return;
            showNewWindowTooltip(link);
        }, true);

        document.addEventListener("mouseout", function (event) {
            var link = closestNewWindowHintLink(event.target);
            if (!link || (event.relatedTarget && link.contains(event.relatedTarget))) return;
            hideNewWindowTooltip();
        }, true);

        document.addEventListener("focusin", function (event) {
            showNewWindowTooltip(closestNewWindowHintLink(event.target));
        }, true);

        document.addEventListener("focusout", function (event) {
            if (closestNewWindowHintLink(event.target)) {
                hideNewWindowTooltip();
            }
        }, true);

        document.addEventListener("keydown", function (event) {
            if (event.key === "Escape" || event.which === 27) {
                hideNewWindowTooltip();
            }
        }, true);

        window.addEventListener("resize", positionNewWindowTooltip, true);
        window.addEventListener("scroll", positionNewWindowTooltip, true);
    }

    function getLinkLabelText(link) {
        var clone = link.cloneNode(true);
        selectAll(clone, ".new-window-indicator, .sr-only").forEach(function (element) {
            if (element.parentNode) {
                element.parentNode.removeChild(element);
            }
        });

        var text = cleanText(clone.textContent);
        if (!text) {
            var image = link.querySelector("img[alt]");
            text = image ? cleanText(image.getAttribute("alt")) : "";
        }

        return text || cleanText(link.getAttribute("title")) || cleanText(link.getAttribute("href"));
    }

    function ensurePlyrRangeLabel(input, text) {
        if (!input || !input.parentNode) return;

        if (!input.id) {
            input.id = "plyr-range-" + Math.random().toString(36).slice(2);
        }

        var label = null;
        var labels = document.querySelectorAll("label[for]");
        for (var i = 0; i < labels.length; i += 1) {
            if (labels[i].getAttribute("for") === input.id && labels[i].classList.contains("plyr-accessible-range-label")) {
                if (!label) {
                    label = labels[i];
                } else if (labels[i].parentNode) {
                    labels[i].parentNode.removeChild(labels[i]);
                }
            }
        }

        var insertionParent = input.parentNode;

        if (!label) {
            label = document.createElement("label");
            label.className = "plyr-accessible-range-label";
            label.setAttribute("for", input.id);
        }

        if (label.parentNode !== insertionParent) {
            insertionParent.appendChild(label);
        }

        setAttr(label, "id", input.id + "-label");
        setAttr(label, "lang", "he");
        label.textContent = text;

        removeAttr(input, "aria-label");
        setAttr(input, "aria-labelledby", label.id);
        setAttr(input, "title", text);
        setAttr(input, "lang", "he");
    }

    function decorateTargetBlankLinks(root) {
        selectAll(root, 'a[target="_blank"]').forEach(function (link) {
            var rel = (link.getAttribute("rel") || "").split(/\s+/).filter(Boolean);
            if (rel.indexOf("noopener") === -1) rel.push("noopener");
            setAttr(link, "rel", rel.join(" "));
            setAttr(link, "data-new-window-hint", newWindowHintText);

            if (!link.hasAttribute("aria-labelledby")) {
                var label = cleanText(link.getAttribute("aria-label")) || getLinkLabelText(link);
                label = cleanText(label.replace(newWindowText, "").replace("נפתח בלשונית חדשה", ""));
                if (label && label.indexOf("לשונית חדשה") === -1) {
                    setAttr(link, "aria-label", label + " " + newWindowText);
                }
            }

            var title = cleanText(link.getAttribute("title"));
            if (title) {
                title = cleanText(title.replace(newWindowText, "").replace(newWindowHintText, ""));
                if (title) {
                    setAttr(link, "title", title);
                } else {
                    removeAttr(link, "title");
                }
            }

            if (!link.querySelector(".new-window-indicator")) {
                var indicator = document.createElement("span");
                indicator.className = "sr-only new-window-indicator";
                indicator.textContent = " " + newWindowText;
                link.appendChild(indicator);
            }
        });
    }

    function labelForms(root) {
        selectAll(root, "form").forEach(function (form) {
            if (form.hasAttribute("aria-label") || form.hasAttribute("aria-labelledby") || form.hasAttribute("title")) return;

            var name = form.getAttribute("name") || "";
            var id = form.getAttribute("id") || "";
            var label = "טופס באתר משה שרון";

            if (form.classList.contains("newsletter-form")) label = "טופס הרשמה לניוזלטר";
            else if (name === "loginForm" || id.toLowerCase().indexOf("login") !== -1) label = "טופס התחברות";
            else if (name === "registerForm" || id.toLowerCase().indexOf("register") !== -1) label = "טופס הרשמה";
            else if (id === "hiddenTimeForm") label = "טופס שמירת זמן צפייה";
            else if (id === "resetPasswordForm") label = "טופס איפוס סיסמא";
            else if (form.closest("#subscription")) label = "טופס המשך לתשלום מנוי";

            setAttr(form, "aria-label", label);
        });
    }

    function hideDecorativeIcons(root) {
        selectAll(root, 'i.material-icons, i[class^="mdi"], i[class*=" mdi"], i[class^="fa"], i[class*=" fa"]').forEach(function (icon) {
            setAttr(icon, "aria-hidden", "true");
            setAttr(icon, "lang", "en");
            setAttr(icon, "translate", "no");
        });

        selectAll(root, "svg:not([role='img']):not([aria-label]):not([aria-labelledby])").forEach(function (svg) {
            setAttr(svg, "aria-hidden", "true");
            setAttr(svg, "focusable", "false");
        });
    }

    function fixIframes(root) {
        selectAll(root, "iframe").forEach(function (frame) {
            var src = frame.getAttribute("src") || "";
            var title = frame.getAttribute("title") || "";
            var hidden = isHidden(frame);

            if (frame.getAttribute("tabindex") === "-1") {
                removeAttr(frame, "tabindex");
            }

            if (!title.trim() || title === "Player for " || title === "Player for video" || /^נגן מדיה:\s*$/.test(title)) {
                if (src.indexOf("youtube.com") !== -1 || src.indexOf("youtube-nocookie.com") !== -1) {
                    setAttr(frame, "title", "נגן וידאו");
                } else if (src.indexOf("recaptcha") !== -1) {
                    setAttr(frame, "title", "reCAPTCHA");
                } else if (hidden) {
                    setAttr(frame, "title", "מסגרת טכנית נסתרת");
                } else {
                    setAttr(frame, "title", "מסגרת תוכן");
                }
            }

            if (hidden) {
                setAttr(frame, "aria-hidden", "true");
            } else if (frame.getAttribute("aria-hidden") === "true") {
                removeAttr(frame, "aria-hidden");
            }
        });
    }

    function translatePlyr(root) {
        selectAll(root, '[data-plyr="seek"]').forEach(function (input) {
            ensurePlyrRangeLabel(input, "התקדמות הנגן");
        });

        selectAll(root, '[data-plyr="volume"]').forEach(function (input) {
            ensurePlyrRangeLabel(input, "עוצמת שמע");
        });

        selectAll(root, ".plyr__sr-only, .label--pressed, .label--not-pressed, .plyr__menu__container span, .plyr__menu__value, progress").forEach(function (element) {
            var text = (element.childNodes.length === 1 ? element.textContent : "").trim();
            if (plyrTextMap[text]) {
                element.textContent = plyrTextMap[text];
                setAttr(element, "lang", "he");
            } else if (text === "undefined") {
                element.textContent = "לא זמין";
                setAttr(element, "lang", "he");
            } else if (/^[A-Za-z0-9_. -]+$/.test(text) && text) {
                setAttr(element, "lang", "en");
                setAttr(element, "translate", "no");
            }
        });
    }

    function normalize(root) {
        decorateTargetBlankLinks(root);
        labelForms(root);
        hideDecorativeIcons(root);
        fixIframes(root);
        translatePlyr(root);
    }

    function init() {
        installFocusStyles();
        trackKeyboardFocus();
        installNewWindowHint();
        normalize(document);

        if ("MutationObserver" in window) {
            var observer = new MutationObserver(function (mutations) {
                mutations.forEach(function (mutation) {
                    if (mutation.type === "childList") {
                        mutation.addedNodes.forEach(function (node) {
                            if (node.nodeType === 1) normalize(node);
                            else if (node.nodeType === 3 && node.parentElement) normalize(node.parentElement);
                        });
                    } else if (mutation.type === "attributes" && mutation.target.nodeType === 1) {
                        normalize(mutation.target);
                    } else if (mutation.type === "characterData" && mutation.target.parentElement) {
                        normalize(mutation.target.parentElement);
                    }
                });
            });
            observer.observe(document.documentElement, {
                childList: true,
                subtree: true,
                characterData: true,
                attributes: true,
                attributeFilter: ["aria-label", "class", "hidden", "style", "tabindex", "title"]
            });
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
