import axiosInstance from "./axiosInstance";

type UpdateCustomerData = {
    id: number;
    email?: string;
    phone?: string;
};

type SavePreferredLayout = {
    overviewPreference: string[];
}

export async function updateCustomerData(data: UpdateCustomerData) {
    const res = await axiosInstance.patch("/customers", data);
    return res.data;
}

export async function updateCustomerOverviewLayout(id: number, overviewPreference: string[]): Promise<SavePreferredLayout> {
    try {
        const res = await axiosInstance.put(`/customers/${id}`, {overviewPreference,});
        return res.data;
    } catch (error) {
        console.error("UPDATE FAILED:", error);
        throw error;
    }
}

export interface RegisterCustomerRequest {
    name: string;
    email: string;
    personalNum: string;
    phoneNumber?: string;
}

/** Registers a new customer. On success the backend also logs the customer in via cookie. */
export async function register(data: RegisterCustomerRequest): Promise<void> {
    await axiosInstance.post("/auth/register", data);
}