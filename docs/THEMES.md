# Appearance

Use the moon/sun icon in the top-right corner, after Sign in or Sign out, to switch the whole application between light and dark. Account screens also show the icon in their top-right header.

The first visit follows the device appearance. A selection is remembered in this browser under `devsync-theme` and shared with other tabs on the same origin. Clear site preferences to follow the device again. Preferences are not stored in the account database or synchronized across devices.

Palette variables and shared form/editor rules live in `src/DevSync.Web/Assets/styles/site.css`. Use the shared `form-input`, `form-button`, semantic palette and surface classes for new screens. Avoid fixed white backgrounds and dark text colors in new components. Preserve original colors for uploaded images.

`wwwroot/js/theme.js` runs before the stylesheet in both layouts. It emits `devsync:theme-change` for visual integrations. Monaco listens to that event and updates its palette without modifying document content. Theme controls use accessible labels, pressed state, keyboard focus and reduced-motion support.

Rebuild with `docker compose up -d --build --wait` after changing styles. The build runs application and client tests. Theme behavior regressions are in `tests/client/theme.test.mjs`.
