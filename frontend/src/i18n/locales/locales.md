# Language Library

The language library contains all translations used in the application.

## Structure

The JSON is divided into four sections:

### `generic`

Commonly reused terms.

```json
"generic": {
    "save": "Spara",
    "cancel": "Avbryt"
}
```

### `errors`

General error messages.

```json
"errors": {
    "loading": "Någonting gick fel vid hämtning av datan."
}
```

### Routes

Text specific to a route.

Naming convention:

```text
"routename-route"
```

Example:

```json
"welcome-route": {
    "title": "Kundportalen",
    "paragraph": "...",
    "button": "Logga in"
}
```

### Components

Text specific to a component.

The key should match the component name in kebab-case.

```text
PageHeader.tsx → "page-header"
PageNavigation.tsx → "page-navigation"
```

Example:

```json
"page-header": {
    "help-center": "Hjälpcenter",
    "settings": "Inställningar",
    "logout": "Logga ut"
}
```

## Usage

import { useTranslation } from "react-i18next";

1. Import useTranslation:
    import { useTranslation } from 'react-i18next';

2. Get the translation function (t):
    const {t} = useTranslation();

3. Use t() with the translation key:
    t("key.key") ex. t("welcome-route.title")

4. To add a new text, add the same key to BOTH language files:
    sv.json & en.json


## Where should a translation go?

* **Reusable term** → `generic`
* **General error** → `errors`
* **Route-specific text** → `<route-name>-route`
* **Component-specific text** → `<component-name>`

Avoid duplicating translations. Reuse an existing `generic` translation when possible.
