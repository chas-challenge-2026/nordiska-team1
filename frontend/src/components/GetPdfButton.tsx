import { useTranslation } from "react-i18next";

type GetPdfBtnProps = {
    accountNum?: string;
}

export default function GetPdfBtn({ accountNum }: GetPdfBtnProps) {

    const {t} = useTranslation();

    const handleClick = () => {
        // const pdfUrl = "/mock/Risk-spotting-cheat-sheet.pdf";
        // window.open(pdfUrl, "_blank");

    };

    return (
        <button 
            onClick={handleClick}
            className="cursor-pointer bg-nordiska-blue font-montserrat text-white p-1 w-fit self-start rounded-xl border-2 border-nordiska-blue transition-colors duration-200 hover:bg-white hover:text-nordiska-blue">
                {t("get-pdf-button.text")}
                {/* to avoid Lint error  */}  {accountNum} 
        </button>
    );
}