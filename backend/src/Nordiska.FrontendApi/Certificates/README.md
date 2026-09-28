## README - FPTestcert5_20240610
* Hämtat från https://developers.bankid.com/test-portal/test-information
Den här mappen innehåller tre olika format av samma certifikat för test.
Lösenordet för samtliga är: `qwerty123`

 **Så väljer du rätt fil:**

* Använd `FPTestcert5_20240610.p12` för nya applikationer och miljöer som stöder modern kryptering. Vi rekommenderar dig att välja den här i första hand. 
* Använd `FPTestcert5_20240610.pem` om din applikation kräver PEM-format.  
* Använd `FPTestcert5_20240610-legacy.pfx ` för äldre applikationer som kräver äldre algoritmer, som Windows Server tidigare versioner än 2022. 


**1. FPTestcert5_20240610.p12:**

* Den här filen lagrar certifikatet och den privata nyckeln i PKCS#12-format.
* Den krypteras med AES-256-CBC-algoritmen som har högre säkerhet än äldre metoder.
* När du skapar ditt certifikat för produktion med BankID Keygen kommer det skapas i det här formatet. 

**2. FPTestcert5_20240610.pem:**

* Den här filen innehåller certifikatet och den krypterade privata nyckeln i PEM-format. 
* Certifikatet ligger i början av filen, följt av den privata nyckeln.

**3. FPTestcert5_20240610-legacy.pfx:**

* Den här filen lagrar certifikatet och den privata nyckeln i PKCS#12-format.
* Den krypteras med den äldre algoritmen ”pbeWithSHA1And40BitRC2-CBC” av kompatibilitetsskäl.
* Den här krypteringsmetoden anses svag och den bör bara användas för äldre applikationer som inte stöder moderna algoritmer. Vi rekommenderar att du använder filen `FPTestcert5_20240610.p12` om det är möjligt.



**Vanligt fel** 

Om du valt p12-filen för en miljö som inte klarar av den är det vanligt att få felet "fel lösenord". Se över ditt val av fil om du får det felmeddelandet. 




This folder contains same certificate for test in three different formats. The password is the same for all of them; `qwerty123`

 **How to select the correct file:**

* Use `FPTestcert5_20240610.p12` for newer applications and environments that support modern encryption methods. We recommend this file as your primary choice. 
* Use `FPTestcert5_20240610.pem` if your application requires PEM format.  
* Use `FPTestcert5_20240610-legacy.pfx ` for older applications requiring older algorithms such as Windows Server earlier versions than 2022. 


**1. FPTestcert5_20240610.p12:**

* This file stores the certificate and the private key in a PKCS#12 format.
* It is encrypted using the AES-256-CBC-algorithm, which has a higher security level than older methods. 
* When you create your certificate for production with the BankID Keygen it will be created in this format. 

**2. FPTestcert5_20240610.pem:**

* This file contains the certificate and the encrypted private key in a PEM format. 
* The certificate is placed in the beginning of the file, followed by the private key. 

**3. FPTestcert5_20240610-legacy.pfx:**

* This file stores the certificate and the private key in a PKCS#12 format.
* For compatibility reasons it’s encrypted using the older algorithm ”pbeWithSHA1And40BitRC2-CBC”.
* This encryption method is considered weak and should only be used for older applications that don’t support modern algorithms. We recommend that you select the file `FPTestcert5_20240610.p12` if possible.



**Common error** 

If you are using the p12 file in an environment that can’t handle it, it’s common to get the error message ”wrong password”. You might need to re-evaluate you file choice if you get this error message.  