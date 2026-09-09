import PageHeader from "../components/PageHeader";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";

export default function PageNotFound() {
    const {t} = useTranslation();

    return (
        <div className='fixed z-[-2]'>
            <PageHeader navLinks={false} fixedPos/>
            <main className='flex items-center h-[100vh] w-[100vw]'>
                <div className='flex flex-col gap-10 ml-[20%] text-dark-navy'>
                    <h1 className='text-7xl font-bold font-montserrat-alternates'>
                        {`404 - ${t("404-route.title")}`}
                    </h1>
                    <p className='text-xl w-[80%] font-montserrat'>
                        {t("404-route.paragraph")}
                    </p>

                <Link 
                    to="/"
                    className='group w-fit flex items-center hover:bg-login-bg cursor-pointer p-8 font-montserrat bg-nordiska-blue text-2xl font-bold h-14 rounded-tr-[10px] rounded-br-[10px] rounded-bl-[10px] text-white pr-10'>
                        <span><img src="icons/arrow-right.svg" alt="" className='invert rotate-180 w-[36px] group-hover:animate-bounce-right mr-5' /></span>
                        {t("404-route.button")}
                </Link>
                </div>
            </main>
        </div>
    );
}