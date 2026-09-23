import { Component, type ErrorInfo, type ReactNode } from "react";
import ServerError from "../pages/ServerError";

type Props = { children: ReactNode };
type State = { hasError: boolean };

export default class ErrorBoundary extends Component<Props, State> {
    state: State = { hasError: false };

    static getDerivedStateFromError(): State {
        return { hasError: true };
    }

    componentDidCatch(error: Error, info: ErrorInfo) {
        console.error(error, info.componentStack);
    }

    render() {
        return this.state.hasError ? <ServerError /> : this.props.children;
    }
}
