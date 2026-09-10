import axiosInstance from "./axiosInstance";

export async function getAccounts() {
    const res = await axiosInstance.get("/savingsaccounts");
    console.log(res.data)
}

export async function createAccount(accountNumber: string, accountType: string, customerId: number) {
    const res = await axiosInstance.post("/savingsaccounts", {
        accountNumber: accountNumber,
        accountType: accountType, 
        customerId: customerId,
    });
    console.log(res.data);
}
