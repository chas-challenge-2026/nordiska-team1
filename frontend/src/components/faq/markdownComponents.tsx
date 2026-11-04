import type { Components } from "react-markdown";

export const markdownComponents: Components = {
    p: (props) => <p className="mb-2 last:mb-0" {...props} />,
    ul: (props) => <ul className="mb-2 list-disc pl-6" {...props} />,
    ol: (props) => <ol className="mb-2 list-decimal pl-6" {...props} />,
    strong: (props) => <strong className="font-semibold" {...props} />,
    h1: (props) => <h3 className="mb-2 text-lg font-semibold" {...props} />,
    h2: (props) => <h3 className="mb-2 text-lg font-semibold" {...props} />,
    h3: (props) => <h3 className="mb-2 text-lg font-semibold" {...props} />,
    a: (props) => (
        <a
            className="underline"
            target="_blank"
            rel="noopener noreferrer"
            {...props}
        />
    ),
};
