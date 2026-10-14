import { useTranslation } from "react-i18next"
import NewTicketButton from "../inbox/NewTicket";

export default function FaqContact() {
    const { t } = useTranslation();

    return (
        <article className="flex flex-col gap-2 sm:gap-5 sm:flex-row sm:justify-between sm:items-center bg-white pl-4 pt-2 pb-2 shadow-md sm:p-5 sm:col-span-2 ">
            <p className="text-xs">Ifall du inte hittar svar på din fråga kan du använda dessa kontaktvägar!</p>
            <div>
                <a href="tel:0771123456" className="group flex items-center">
                    <img className="h-5 w-5 mr-2 bg-primary-blue mask-[url('/icons/phone.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-nordiska-blue" />
                    <span className="font-semibold text-sm cursor-pointer text-primary-blue group-hover:text-nordiska-blue">
                        0771-123 456
                    </span>
                </a>
                <p className="text-xs ml-7">{t("faq.faq-sidebar.phone-specs")}</p>
            </div>
            <NewTicketButton className="self-start sm:self-auto" />
        </article>
    )
}
