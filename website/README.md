# Cut The Pause website

This is a dependency-free static marketing site for the Cut The Pause desktop beta. It is intentionally a folder of HTML, CSS, JavaScript, and copied repository assets: no framework, backend, analytics, tracker, account flow, or payment provider is required.

## Local preview

From the repository root:

```bash
python3 -m http.server 41739 --directory website
```

Open <http://127.0.0.1:41739/>. A static server is recommended because it matches how free hosts serve the site and avoids browser restrictions around local asset paths.

## Configure release or checkout

`script.js` contains the reviewed public beta release URL. Download links open
that GitHub release, while `checkoutUrl` remains empty and checkout stays a
disabled preview control until a real payment provider exists. If a future
deployment changes either flow, set only a reviewed `https://` or `mailto:` URL
in `window.CUT_THE_PAUSE_CONFIG`.

## Free static hosting

The `website/` directory is the publish directory for the repository-hosted
Pages workflow and can also be used on any static host with a free tier. Keep
the copied assets alongside `index.html`, `styles.css`, and `script.js`, and
verify the generated site locally before publishing.

## Content source of truth

Product and install claims mirror the repository README, `docs/INSTALL_MACOS.md`, `packaging/macos/INSTALL_MACOS.md`, and `docs/COMMERCIAL_LICENSING.md`. The site labels the beta as unsigned and not notarized, uses the reviewed release URL, and does not connect a checkout provider.
