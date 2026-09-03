const FOOTER_LINKS = [
  { title: "Integritetspolicy", href: "https://www.nordiska.se/" },
  { title: "Tillgänglighet", href: "https://www.nordiska.se/" },
  { title: "Cookies", href: "https://www.nordiska.se/" },
  { title: "Cookie-inställningar", href: "https://www.nordiska.se/" },
];

/**
 * Footer för applikationen.
 * Länkarna pekar tillfälligt alla på nordiska.se tills respektive sida finns.
 */

export default function PageFooter() {
  const year = new Date().getFullYear();

  return (
    <footer className="w-full bg-dark-navy px-10 py-8">
      <div className="h-px w-full bg-light-blue-accent/40 mb-6" />

      <div className="flex flex-wrap items-center justify-between gap-4 text-sm text-light-blue-accent">
        <p>
          © {year} Bankaktiebolaget Nordiska (publ), 556760-6032. All rights
          reserved. · nordiska.se
        </p>

        <nav className="flex items-center gap-6">
          <a
            href="https://www.nordiska.se/"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="LinkedIn"
            className="text-light-blue-accent hover:text-nordiska-orange transition-colors duration-200"
          >
            <svg
              className="w-5 h-5"
              viewBox="0 0 24 24"
              fill="currentColor"
              xmlns="http://www.w3.org/2000/svg"
            >
              //skapar LinkedIn loggan med rätt färger.
              <path d="M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.446-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433a2.062 2.062 0 1 1 0-4.125 2.062 2.062 0 0 1 0 4.125zM7.114 20.452H3.558V9h3.556v11.452z" />
            </svg>
          </a>

          {FOOTER_LINKS.map(({ title, href }) => (
            <a
              key={title}
              href={href}
              target="_blank"
              rel="noopener noreferrer"
              className="hover:text-nordiska-orange transition-colors duration-200"
            >
              {title}
            </a>
          ))}
        </nav>
      </div>
    </footer>
  );
}
