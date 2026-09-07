import PageHeader from "../components/PageHeader";
import { useTranslation } from "react-i18next";

export default function LoginPage() {
    const { t } = useTranslation();

    return (
        <div className="fixed z-[-2] bg-login-bg min-h-screen w-full">
            <PageHeader login navLinks={false} />
            <main className="grid grid-cols-2 min-h-[calc(100vh-116px)] items-center">
                <section className="flex flex-col items-end pr-12">
                    <div className="text-right text-white">
                        <h1 className="text-7xl font-bold font-montserrat-alternates">
                            {t("login-route.title")}.
                        </h1>

                        <p className="text-xl w-[500px] font-montserrat mt-5">
                            {t("login-route.paragraph")}
                        </p>
                    </div>
                </section>
                <section className="border-l-2 border-[#F6B900] pl-12">
                    <div className="h-[30vh] w-[320px] bg-white rounded-tr-[20px] rounded-br-[20px]">
                        <p>Inlogg med BankID här</p>
                    </div>
                </section>
            </main>
        </div>
    );
}