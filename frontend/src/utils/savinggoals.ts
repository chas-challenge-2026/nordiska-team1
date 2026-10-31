    export default function progressColor(percent: number){
        if (percent >= 100){return "bg-[#16A34A]"} // #16A34A
        else if (percent > 80) return "bg-[#CCC703]" // #CCC703
        else if (percent > 50) return "bg-[#EAB308]" // #EAB308
        else if (percent > 30) return "bg-[#F59E0B]" // #F59E0B
        else return "bg-[#F97316]" // #F97316
    }