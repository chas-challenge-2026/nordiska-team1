import PageHeader from '../components/PageHeader'

type pageProps = {
    inactive?: boolean
}

export default function LandingPage({ inactive = false }: pageProps) {

    return (
        <div className='fixed z-[-2] bg-nordiska-blue'>
            <div className='fixed z-[-1] bg-[url("src/assets/img/mountain_view.webp")] bg-cover bg-center h-[100vh] w-[100vw] opacity-65'>
            </div>
            <PageHeader navLinks={false} fixedPos />
            <div className='flex items-center h-[100vh] w-[100vw]'>
                <div className='flex flex-col gap-10 ml-[20%] text-white'>
                    <h1 className='text-7xl font-bold font-montserrat-alternates'>
                        {inactive ? "Du är utloggad." : "Kundportalen."}
                    </h1>
                    <p className='text-xl w-[80%] font-montserrat'>
                        {inactive ?
                            "På grund av inactivitet har du av säkerhetsskäl blivit utloggad."
                            : "För en samlad överblick över dina konton och ditt sparande."}
                    </p>
                    <button className='group flex justify-between items-center hover:bg-login-bg cursor-pointer p-8 font-montserrat bg-nordiska-blue w-[60%] text-2xl font-bold h-14 rounded-tr-[10px] rounded-br-[10px] rounded-bl-[10px]'>
                        {inactive ? "Logga in igen" : "Logga in"}
                        <span><img src="icons/arrow-right.svg" alt="" className='invert w-[36px] group-hover:animate-bounce-right' /></span>
                    </button>
                </div>
            </div>
        </div>
    )
}
