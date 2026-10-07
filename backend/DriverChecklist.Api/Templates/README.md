# Mallmapp (lokalt alternativ)

Den här mappen är tom och committas inte med riktiga Excel-mallar (de innehåller
företagsspecifik layout och ska inte ligga i Git).

Som standard läser API:et mallarna från den inloggade användarens **Skrivbord**
(`Templates:Path` i `appsettings.json` är tom). Vill du istället testa lokalt med
mallarna i den här mappen, sätt `Templates:Path` i `appsettings.Development.json`
till den absoluta sökvägen till den här mappen.

Förväntade filnamn (se `Services/TemplateResolver.cs`):

- `Ny 1 Saltsyra , Pix, mm. Tankar MED skyddande beläggning.xlsx` (Typ 1)
- `Ny 2 Svavelsyra 94,97 och 98 Fennosize. Tankar UTAN skyddande beläggning.xlsx` (Typ 2)
- `Mall_Checklista_Typ3.xlsx` (Typ 3 - ALS/LUT, tillkommer senare)

När mallarna flyttas till en delad Teams/OneDrive-kanal, peka `Templates:Path`
dit istället. Filerna öppnas alltid read-only med delad åtkomst
(`FileShare.ReadWrite`), se README.md avsnitt 7.
