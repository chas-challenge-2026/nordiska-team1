import { Outlet } from "react-router";
import PageNavigation from "../components/PageNavigation";
// import PageFooter from "../components/PageFooter";

export default function DesktopLayout() {
  return (
    <div className="flex min-h-dvh flex-col">
      <PageNavigation />

      {/* ANIMATION PÅ DETTA */}
      <main className="flex flex-1 w-full pb-[60px] md:pb-10">
        <Outlet />
      </main>

      {/* <PageFooter /> */}
    </div>
  );
}
