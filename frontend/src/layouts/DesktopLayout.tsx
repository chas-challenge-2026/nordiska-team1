import { Outlet } from "react-router";
import PageHeader from "../components/PageHeader";
import PageNavigation from "../components/PageNavigation";
import PageFooter from "../components/PageFooter";

export default function DesktopLayout() {
  return (
    <div className="h-screen flex flex-col">
      <PageHeader />
      <PageNavigation />
      <div className="flex flex-1 min-h-0 w-screen">
        <Outlet />
      </div>
      <PageFooter />
    </div>
  );
}
