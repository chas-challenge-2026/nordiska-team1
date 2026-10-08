import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";

type StatusMessageProps = {
    title?: string;
    message?: string;
};

export function LoadingState({ title, message}: StatusMessageProps) {
    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-15 w-15 animate-[spin_3s_steps(8)_infinite] bg-nordiska-orange mask-[url('/icons/throbber.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            {title && (<p className="font-semibold mt-1 text-dark-navy/80"> {title} </p>)}
            {message && (<p className="font-semibold mt-1 text-dark-navy/80"> {message} </p>)}
        </div>
    );
}

export function ErrorState({ title, message}: StatusMessageProps) {
    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-15 w-15 bg-red-700 mask-[url('/icons/frown.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            {title && (<p className="font-semibold mt-1 text-dark-navy/80"> {title} </p>)}
            {message && (<p className="font-semibold mt-1 text-dark-navy/80"> {message} </p>)}
        </div>
    );
}

export function NoPlannedState() {
    const {t} = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-16 w-16 bg-nordiska-orange mask-[url('/icons/transfers.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            <p className="xl:text-lg font-semibold mt-1 text-dark-navy/80"> {t("no-planned-state.title")} </p>
            <p className="px-10 text-center text-sm mt-1 text-dark-navy/80"> {t("no-planned-state.description")}</p>
            <p className="mt-4 text-[0.9em] xl:text-base font-semibold text-dark-navy/80 md:mt-6">{t("no-planned-state.action")} <span onClick={() => navigate("/transfer")} className="text-primary-blue cursor-pointer hover:text-nordiska-blue">{t("page-navigation.transfers")}</span></p>
        </div>
    );
}

export function NoSavingGoalState() {
    const {t} = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-9">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-15 w-15 bg-nordiska-orange mask-[url('/icons/target.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            <p className="xl:text-lg font-semibold mt-1 text-dark-navy/80">{t("no-saving-goal-state.title")}</p>
            <p className="px-10 text-center text-sm mt-1 text-dark-navy/80"> {t("no-saving-goal-state.description")} </p>
            <p className="mt-4 text-[0.9em] xl:text-base font-semibold text-center text-dark-navy/80 md:mt-6">{t("no-saving-goal-state.action")} <span onClick={() => navigate("/accounts")} className="inline-block text-primary-blue cursor-pointer hover:text-nordiska-blue">{t("page-navigation.accounts")}</span></p>
        </div>
    );
}