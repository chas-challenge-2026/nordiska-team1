import { useTranslation } from "react-i18next"
import NewTicketButton from "../inbox/NewTicket";

export default function FaqContact() {
    const { t } = useTranslation();

    return (
        <article className="flex flex-col gap-2 sm:gap-5 sm:flex-row sm:justify-between sm:items-center bg-white pl-4 pt-2 pb-2 shadow-md sm:p-5 sm:col-span-2 ">
            <p className="text-xs mr-10 mt-2 sm:mr-0 sm:mt-0">{t("faq.faq-sidebar.contact-us")}</p>
            <div className="flex items-center"> 
                <a href="tel:0771123456" className="group flex items-center">
                    <img className="h-3.5 w-3.5 mr-1 bg-primary-blue mask-[url('/icons/phone.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-primary-hover" />
                    <span className="font-medium text-sm cursor-pointer text-primary-blue group-hover:text-primary-hover">
                        0771-123 456
                    </span>
                </a>
                <p className="text-xs ml-2">{t("faq.faq-sidebar.phone-specs")}</p>
            </div>
            <div className="flex items-center"> 
                <a href="mailto:support@nordiska.se" className="group flex items-center">
                    <img className="h-4 w-4 mr-1 bg-primary-blue mask-[url('/icons/envelope.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-primary-hover" />
                    <span className="font-medium text-sm cursor-pointer text-primary-blue group-hover:text-primary-hover">
                        support@nordiska.se
                    </span>
                </a>
            </div>
            <NewTicketButton className="self-start sm:self-auto" />
        </article>
    )
}