import axiosInstance from "./axiosInstance";

type UpdateCustomerData = {
    id: number;
    email?: string;
    phone?: string;
};

export async function updateCustomerData(data: UpdateCustomerData) {
    const res = await axiosInstance.patch("/customers", data);
    return res.data;
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