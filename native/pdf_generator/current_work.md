the point of this branch is to get the PDFS in a state where they can be signed where we can call the signer We need to include the signer module as dependency / .so /  ??? / how best to call it? 

and then we need to actually call it and then we need to write that stuff to the PDF and then we go then we should have a PDF that actually asks is signed Uh in the beginning now the uh we're just getting a placeholder key but that's that's fine we just want to see that we write to the correct spot in the PDF and stuff like that

issues from linear are: 
NOR-198
Hash rendered PDFs and append signature placeholder to rendered PDFs for deferred signing
rendered PDFS needs to be hashed before we can  sign them, we also need to apppend all the stuff needed for deferred signing to the PDF file.

NOR-193
Skriv CMS-resultat till /Contents
Ta emot hex-encodad CMS från C, kontrollera att den får plats i placeholdern och skriv den till /Contents. Fyll kvarvarande utrymme med 0.

NOR-192
Bygg signeringsklar PDF och beräkna digest
Reservera /Contents, sätt korrekt ByteRange och hasha de bytes som ska signeras. Skicka algoritm, digest och längd till C-API:t.