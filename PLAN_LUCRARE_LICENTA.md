# Plan structurat pentru lucrarea de licenta

## Titlu de lucru

**OurSpace AI: platforma sociala completa cu prietenii, feed, mesagerie privata si profiluri web personalizabile asistate de AI**

## 1. Logica de business si motivatia proiectului

Platformele sociale clasice ofera utilizatorilor optiuni limitate de personalizare. Proiectul propune o platforma inspirata de MySpace, adaptata la tehnologiile actuale, in care utilizatorii isi pot crea un profil web personalizat prin descrieri naturale si prin editarea directa a codului generat.

Valoarea principala a produsului este reducerea barierei tehnice dintre utilizator si dezvoltarea web. Un utilizator poate descrie stilul si continutul dorit, iar sistemul genereaza o interfata, verifica automat codul, identifica riscurile si propune corectii inainte de publicare. Pentru utilizatorii tehnici, platforma pastreaza controlul asupra codului HTML, CSS si React/Next.js.

Din perspectiva academica, proiectul reuneste trei directii aplicate:

- inteligenta artificiala pentru generare, validare, optimizare si analiza a profilurilor;
- securitate pentru cod generat, identitate, comunicare si publicare de continut;
- arhitectura software si DevOps pentru servicii independente, observabilitate si scalare.

Rezultatul urmarit este un prototip functional si evaluabil, nu o platforma comerciala completa. Extinderea cerintelor poate fi facuta dupa rezultate experimentale si cercetare suplimentara.

Platforma finala include obligatoriu relatii de prietenie, postari si feed, precum si mesagerie privata. Fluxul de generare/publicare a profilurilor este primul milestone tehnic, nu intregul produs. Aceste capabilitati sociale vor avea servicii separate, fiecare modular intern si pregatit pentru integrare prin API-uri si evenimente versionate.

## 2. Obiective generale

- [ ] Definirea cerintelor functionale si nefunctionale ale platformei.
- [ ] Implementarea unei platforme web pentru creare, editare, previzualizare si publicare de profiluri.
- [ ] Implementarea unui flux AI iterativ: `generare -> compilare -> validare -> corectie -> recompilare`.
- [ ] Evaluarea codului generat din punct de vedere functional, semantic si de securitate.
- [ ] Separarea sistemului in servicii independente, cu comunicare documentata intre ele.
- [ ] Containerizarea, automatizarea livrarii si monitorizarea aplicatiei.
- [ ] Definirea unor experimente reproductibile si interpretarea rezultatelor.

## 3. Scope si prioritizare

### 3.1 Scope obligatoriu pentru versiunea de baza

- [ ] Autentificare si autorizare pentru utilizatori.
- [ ] Creare profil prin prompt si editare manuala.
- [ ] Generare de cod pentru o suprafata controlata, de exemplu componente React si CSS.
- [ ] Previzualizare izolata a profilului.
- [ ] Validator HTML/CSS/React si scor semantic.
- [ ] Iteratii automate de corectie cu limita de executii si criterii de oprire.
- [ ] Scanare de securitate inainte de publicare.
- [ ] Salvare versiuni si posibilitate de revenire la o versiune anterioara.
- [ ] Publicare controlata a profilului.
- [ ] Docker, pipeline CI/CD, logare si monitorizare de baza.

- [ ] Cereri de prietenie, acceptare, eliminare si blocarea utilizatorilor.
- [ ] Postari cu audienta explicita si feed paginat, cu verificarea accesului curent.
- [ ] Mesagerie privata directa, istoric persistent si livrare autentificata in timp real.
- [ ] Servicii separate pentru social graph, continut, feed si mesagerie; integrare fiabila si autorizata.

### 3.2 Directii optionale, conditionate de cercetare

- [ ] Sistem multi-agent specializat.
- [ ] Model predictiv pentru succesul unui profil.
- [ ] Clasificator ML pentru riscul codului generat.
- [ ] Autorepair pentru probleme de securitate cu verificare ulterioara.
- [ ] Criptare end-to-end pentru mesageria privata obligatorie; criptarea avansata este extensia optionala, nu mesageria in sine.
- [ ] Semnatura digitala RSA pentru artefactele publicate.
- [ ] Orchestrare Kubernetes intr-un mediu local sau cloud.

Aceste directii nu trebuie sa blocheze MVP-ul. Fiecare se introduce numai dupa definirea datelor disponibile, a criteriilor de evaluare si a costului de implementare.

## 4. Organizarea pe specializari

## A. Analiza de business, produs si cercetare

### Task-uri

- [ ] Definirea tipurilor de utilizatori: creator non-tehnic, creator tehnic, moderator si administrator.
- [ ] Documentarea cazurilor de utilizare principale: creare, regenerare, editare, previzualizare, publicare si raportare.
- [ ] Stabilirea criteriilor de succes pentru produs: timp pana la primul profil, rata de generare valida, rata de publicare si satisfactia utilizatorului.
- [ ] Definirea MVP-ului si a functionalitatilor care raman in backlog.
- [ ] Realizarea unei analize comparative cu platforme sociale si instrumente AI de generare UI.
- [ ] Formularea intrebarilor de cercetare si a ipotezelor experimentale.
- [ ] Definirea unui mecanism de control al modificarilor in cadrul procesului waterfall.
- [ ] Documentarea limitarilor: costuri LLM, date insuficiente, riscuri de securitate si limite de timp.

### Livrabile

- [ ] Document de viziune si scope.
- [ ] Specificatie de cerinte.
- [ ] Matrice de trasabilitate cerinta-test.
- [ ] Registru de riscuri si decizii arhitecturale.

## B. Arhitectura software si backend .NET

### Task-uri

- [ ] Definirea arhitecturii logice si a granitelor dintre servicii.
- [ ] Alegerea comunicarii sincrone si asincrone dintre servicii.
- [ ] Implementarea API Gateway sau a unui punct de intrare echivalent.
- [ ] Implementarea serviciului de identitate si acces.
- [ ] Implementarea serviciului de utilizatori si profiluri.
- [ ] Implementarea serviciului de proiecte, versiuni si publicare.
- [ ] Implementarea serviciului de generare AI si a cozii de joburi.
- [ ] Implementarea serviciului de validare si scanare.
- [ ] Implementarea serviciilor separate social graph, continut, feed si mesagerie privata.
- [ ] Separarea interna domain/application/infrastructure/API in fiecare serviciu si definirea contractelor de comunicare.
- [ ] Integrare prin API-uri autentificate si evenimente versionate, outbox/inbox, deduplicare, retry limitat si reconciliere.
- [ ] Date detinute de serviciul responsabil; fara acces SQL direct intre servicii.
- [ ] Definirea contractelor API, a schemei de erori si a politicilor de retry.
- [ ] Aplicarea validarii inputului, rate limiting si politici de timeout.
- [ ] Implementarea auditului pentru actiuni sensibile.
- [ ] Definirea strategiilor de migrare si backup pentru date.

### Livrabile

- [ ] Diagrama de arhitectura.
- [ ] Specificatii OpenAPI.
- [ ] Codul serviciilor si testele aferente.
- [ ] Document ADR pentru deciziile importante.

## C. Frontend si experienta utilizatorului

### Task-uri

- [ ] Implementarea aplicatiei Next.js.
- [ ] Crearea fluxurilor pentru autentificare, dashboard, editor si previzualizare.
- [ ] Implementarea editorului de prompt si a editorului de cod.
- [ ] Afisarea etapelor de generare, compilare, validare si corectie.
- [ ] Afisarea scorului semantic si a explicatiilor pentru probleme.
- [ ] Implementarea istoricului de versiuni si a actiunilor de rollback.
- [ ] Fluxuri UI pentru prieteni, postari/feed si conversatii private, in aplicatia de incredere, separat de codul generat.
- [ ] Izolarea previzualizarii pentru a preveni executarea continutului neautorizat.
- [ ] Implementarea designului responsive si a starilor de incarcare si eroare.
- [ ] Verificarea accesibilitatii si a utilizabilitatii printr-un set de scenarii.

### Livrabile

- [ ] Wireframe-uri si fluxuri de utilizator.
- [ ] Aplicatie frontend functionala.
- [ ] Raport de testare UX si accesibilitate.

## D. Inteligenta artificiala si generare de cod

### D1. Generare si validare iterativa

- [ ] Definirea formatului de intrare: prompt, preferinte vizuale, continut si constrangeri.
- [ ] Definirea unui format de iesire controlat, de exemplu manifest de componente plus fisiere de cod.
- [ ] Implementarea generarii initiale folosind un LLM.
- [ ] Implementarea compilarii sau a verificarii de tipuri pentru codul generat.
- [ ] Implementarea validatorului HTML pentru structura si conformitate.
- [ ] Implementarea validatorului CSS pentru sintaxa, reguli si conflicte evidente.
- [ ] Implementarea validatorului React/Next.js pentru importuri, componente si reguli de runtime.
- [ ] Implementarea validatorului semantic: acoperirea cerintelor din prompt si scor asociat.
- [ ] Definirea unei liste de erori cu severitate, explicatie si recomandare de corectie.
- [ ] Implementarea buclei de corectie cu numar maxim de iteratii, timeout si criteriu de oprire.
- [ ] Pastrarea rezultatelor fiecarei iteratii pentru reproductibilitate si analiza.

### D2. Sistem multi-agent, daca cercetarea il justifica

- [ ] Definirea unui orchestrator care coordoneaza agentii.
- [ ] Agent planificator pentru structura si descompunerea cerintei.
- [ ] Agent de generare UI pentru componente si layout.
- [ ] Agent de stilizare pentru CSS si design system.
- [ ] Agent de securitate pentru detectarea riscurilor.
- [ ] Agent de optimizare pentru reducerea complexitatii si a dimensiunii codului.
- [ ] Definirea contractului de intrare/iesire si a limitelor fiecarui agent.
- [ ] Compararea sistemului multi-agent cu un flux single-agent pe aceleasi scenarii.

### D3. Analiza succesului profilurilor

- [ ] Definirea metricilor: vizualizari, interactiuni, timp pe profil, reveniri si feedback.
- [ ] Proiectarea schemei de colectare a datelor si a politicii de anonimizare.
- [ ] Extragerea caracteristicilor: numar de sectiuni, layout, paleta, contrast, componente si continut.
- [ ] Stabilirea unei metode de definire a etichetei de succes.
- [ ] Compararea unor modele precum Random Forest, XGBoost si un model de baza.
- [ ] Evaluarea cu separare intre antrenare, validare si testare, evitand scurgerea datelor.
- [ ] Explicarea predictiilor si identificarea limitelor modelului.
- [ ] Folosirea rezultatelor pentru recomandari, nu pentru decizii opace sau garantii de succes.

### Livrabile

- [ ] Designul fluxului AI.
- [ ] Set de scenarii si benchmark.
- [ ] Raport cu metrici: validitate, acoperirea cerintelor, iteratii, latenta si cost.
- [ ] Comparatie experimentala intre abordari.

## E. Securitate aplicata

### Task-uri obligatorii

- [ ] Stocarea parolelor folosind un algoritm modern de hashing si salt.
- [ ] Management securizat pentru tokeni, sesiuni si secrete.
- [ ] Autorizare pe resurse si separarea rolurilor.
- [ ] Protectie impotriva XSS, CSRF, injection, SSRF si path traversal.
- [ ] Sanitizarea si izolarea codului generat in previzualizare.
- [ ] Executarea verificarilor intr-un sandbox cu resurse si timp limitat.
- [ ] Scanarea dependintelor si a imaginilor Docker.
- [ ] Auditarea actiunilor de publicare, modificare si administrare.
- [ ] Modelarea amenintarilor si definirea unui plan de raspuns la incidente.

### Task-uri de cercetare

- [ ] Construirea unui set de exemple safe si malitioase, cu sursa si etichete documentate.
- [ ] Antrenarea si compararea unui clasificator de risc, de exemplu Random Forest sau XGBoost.
- [ ] Definirea claselor de risc si a costului diferit al fals-pozitivelor si fals-negativelor.
- [ ] Combinarea clasificatorului cu reguli statice si analiza dependintelor.
- [ ] Definirea politicii pentru blocare, carantinare, revizie manuala sau autorepair.
- [ ] Validarea oricarei reparatii prin recompilare si rescanare; reparatia nu trebuie acceptata doar pe baza predictiei modelului.
- [ ] Investigarea semnarii RSA a versiunilor publicate si a verificarii integritatii.
- [ ] Investigarea criptarii end-to-end pentru mesagerie, inclusiv managementul cheilor si recuperarea contului.

### Livrabile

- [ ] Threat model si matrice de riscuri.
- [ ] Raport de scanare si teste de securitate.
- [ ] Politici documentate pentru publicare si continut respins.
- [ ] Raport experimental pentru clasificator, daca este implementat.

## F. Date, baze de date si guvernanta datelor

### Task-uri

- [ ] Proiectarea schemelor PostgreSQL pentru utilizatori, profiluri, versiuni si audit.
- [ ] Definirea separarii datelor intre servicii, evitand o baza comuna fara justificare.
- [ ] Implementarea migrarilor versionate.
- [ ] Definirea retentiei pentru prompturi, cod, loguri si date de telemetrie.
- [ ] Anonimizarea datelor folosite in experimente.
- [ ] Implementarea backupului si testarea restaurarii.
- [ ] Definirea politicii pentru stergerea contului si a datelor asociate.
- [ ] Documentarea calitatii si provenientei datelor pentru modelele ML.

## G. DevOps, cloud si infrastructura

### Task-uri

- [ ] Crearea Dockerfile-urilor si a configuratiilor pentru dezvoltare locala.
- [ ] Definirea mediilor: local, test/staging si productie demonstrativa.
- [ ] Configurarea CI pentru build, teste, analiza statica si scanare.
- [ ] Configurarea CD cu aprobari si posibilitate de rollback.
- [ ] Gestionarea configuratiilor si secretelor in afara codului sursa.
- [ ] Definirea health checks, readiness checks si graceful shutdown.
- [ ] Implementarea logurilor centralizate si a corelarii prin correlation ID.
- [ ] Implementarea metricilor pentru latenta, erori, throughput, cost AI si resurse.
- [ ] Implementarea distributed tracing pentru fluxurile dintre servicii.
- [ ] Definirea alertelor si a procedurilor de diagnostic.
- [ ] Evaluarea Kubernetes pentru replicare, self-healing, autoscaling si rolling updates.
- [ ] Documentarea limitelor demonstratiei locale fata de un cluster cloud real.
- [ ] Executarea unor teste controlate de cadere a unui serviciu si verificarea recuperarii.

### Livrabile

- [ ] Pipeline CI/CD.
- [ ] Manifestele Docker si Kubernetes, daca sunt incluse.
- [ ] Dashboard-uri si reguli de alerta.
- [ ] Runbook operational.

## H. Testare si asigurarea calitatii

### Task-uri

- [ ] Definirea strategiei de testare si a criteriilor de acceptanta.
- [ ] Teste unitare pentru reguli de business si validatoare.
- [ ] Teste de integrare pentru API-uri, baze de date si cozi.
- [ ] Teste end-to-end pentru fluxul complet de creare si publicare.
- [ ] Teste de contract intre servicii.
- [ ] Teste pentru prompturi si regresie a rezultatelor AI.
- [ ] Teste de securitate si teste negative pentru inputuri ostile.
- [ ] Teste de performanta pentru generare, previzualizare si publicare.
- [ ] Teste de rezilienta pentru indisponibilitatea serviciilor si a dependintelor.
- [ ] Stabilirea pragurilor de acceptare pentru scor, latenta, rata de eroare si disponibilitate.

## I. Documentatie academica si prezentare

### Task-uri

- [ ] Documentarea stadiului actual al cercetarii si a lucrarilor similare.
- [ ] Motivarea alegerii tehnologiilor si a arhitecturii.
- [ ] Descrierea metodologiei waterfall si a punctelor de control.
- [ ] Documentarea experimentelor, dataseturilor, parametrilor si limitarilor.
- [ ] Pregatirea diagramelor, capturilor si scenariilor de demonstratie.
- [ ] Redactarea concluziilor si a directiilor viitoare.
- [ ] Pregatirea unui ghid de rulare reproductibila a proiectului.

## 5. Plan de executie waterfall

### Faza 0: Initiere si cercetare

- [ ] Confirmarea temei, obiectivelor si criteriilor de evaluare.
- [ ] Studierea literaturii si a solutiilor existente.
- [ ] Stabilirea MVP-ului si a riscurilor majore.
- [ ] Criteriu de iesire: cerintele initiale si planul de cercetare aprobate.

### Faza 1: Analiza si specificarea cerintelor

- [ ] User stories, cazuri de utilizare si cerinte nefunctionale.
- [ ] Cerinte de securitate, date, performanta si observabilitate.
- [ ] Criteriu de iesire: specificatie versionata si matrice de acceptanta.

### Faza 2: Proiectare

- [ ] Arhitectura, contracte API, modele de date si prototip UX.
- [ ] Designul fluxului AI si al pipeline-ului DevOps.
- [ ] Definirea runtime-ului controlat, a izolarii build/preview si a regulilor de publicare; validare printr-un spike tehnic.
- [ ] Criteriu de iesire: proiect tehnic revizuit si decizii documentate.

### Faza 3: Implementarea MVP

- [ ] Backend, frontend, baza de date, autentificare si profiluri.
- [ ] Primul milestone: cod manual -> versiune imuabila -> build izolat -> validare -> preview izolat -> publicare artefact -> rollback.
- [ ] Generare AI si corectie limitata folosind acelasi flux verificat.
- [ ] Milestone social: servicii social graph, continut, feed si mesagerie privata, integrate prin contracte versionate.
- [ ] Verificarea blocarii, confidentialitatii, livrarii duplicate, intarzierii evenimentelor si caderii independente a serviciilor.
- [ ] Criteriu de iesire: scenariile principale functioneaza intr-un mediu local.

### Faza 4: Securizare si automatizare

- [ ] Consolidarea sandbox-ului, scanarii si politicilor de acces implementate din primul milestone; extinderea CI/CD si observabilitatii.
- [ ] Criteriu de iesire: nu exista vulnerabilitati critice cunoscute, iar pipeline-ul este reproductibil.

### Faza 5: Extensii de cercetare

- [ ] Selectarea, pe baza rezultatelor, a sistemului multi-agent, ML pentru succes, clasificatorului de risc sau criptografiei avansate.
- [ ] Criteriu de iesire: fiecare extensie are ipoteza, metoda, baseline si criterii de evaluare.

### Faza 6: Testare si evaluare

- [ ] Rularea testelor functionale, de securitate, performanta si rezilienta.
- [ ] Compararea rezultatelor cu criteriile stabilite.
- [ ] Criteriu de iesire: raport de rezultate si lista de limitari.

### Faza 7: Documentare si prezentare

- [ ] Finalizarea lucrarii, ghidului de instalare si demonstratiei.
- [ ] Criteriu de iesire: proiect reproductibil si prezentare finala pregatita.

## 6. Controlul schimbarilor in model waterfall

Procesul ramane waterfall, dar fiecare faza are o poarta de decizie. O schimbare poate fi acceptata daca:

1. rezolva un risc important sau imbunatateste obiectivul academic;
2. are criterii de evaluare masurabile;
3. nu compromite MVP-ul si calendarul principal;
4. este documentata cu motiv, impact, alternativa respinsa si noul livrabil;
5. este aprobata inainte de a modifica fazele deja inchise.

Pentru fiecare propunere se va completa o fisa scurta:

- descrierea schimbarii;
- motivul si rezultatul cercetarii care o sustine;
- impact asupra timpului, costului si arhitecturii;
- riscuri noi;
- decizia: acceptata, amanata sau respinsa;
- criterii de acceptanta actualizate.

## 7. Criterii finale de succes

- [ ] Un utilizator poate crea si publica un profil pornind de la un prompt.
- [ ] Codul este compilat, validat si verificat inainte de publicare.
- [ ] Erorile sunt explicate si pot declansa iteratii controlate de corectie.
- [ ] Profilurile si datele utilizatorilor sunt protejate prin controale de securitate documentate.
- [ ] Serviciile pot fi pornite reproductibil si sunt monitorizabile.
- [ ] Exista teste pentru fluxurile critice si dovezi ale rezultatelor.
- [ ] Utilizatorii pot gestiona prietenii, publica postari, consulta un feed autorizat si comunica prin mesagerie privata.
- [ ] Serviciile sociale sunt modulare, independente si integrate fara partajarea directa a datelor; confidentialitatea rezista la proiectii intarziate si erori partiale.
- [ ] Orice componenta ML sau multi-agent implementata este comparata cu un baseline.
- [ ] Lucrarea explica limitarile, compromisurile si directiile de dezvoltare ulterioara.

## 8. Baseline pentru implementare

Documentele initiale de implementare sunt:

- [Specificatia MVP](docs/MVP_SPECIFICATION.md)
- [Decizii arhitecturale](docs/ARCHITECTURE_DECISIONS.md)
- [Contracte de domeniu si API](docs/DOMAIN_AND_API_CONTRACTS.md)
- [Ordinea implementarii](docs/IMPLEMENTATION_ROADMAP.md)
- [Arhitectura platformei sociale si integrarea serviciilor](docs/SOCIAL_PLATFORM_ARCHITECTURE.md)

Aceste documente definesc baseline-ul initial; deciziile deschise necesita dovezi tehnice. Izolarea codului nefiabil este obligatorie de la primul build si preview, nu o completare amanata pentru faza de securizare.

## 9. Flexibilitate si evolutia capabilitatilor

Structura proiectului va separa formatul de authoring, contractul arborelui de profil, politica de capabilitati, validarea, renderer-ul si asamblarea artefactelor. Subsetul JSX/CSS verificat in spike este o limita initiala, nu o lista definitiva de functionalitati si nu un sablon vizual obligatoriu.

Contractele si politicile vor fi versionate explicit. Extinderea sau restrangerea capabilitatilor necesita dovezi relevante de securitate, performanta si compatibilitate, criterii de acceptanta si actualizarea documentatiei. Reviziile si artefactele existente raman imuabile; migrarile produc versiuni noi, iar revocarea de securitate poate bloca artefactele nesigure.

Cerintele concrete de structurare, compatibilitate si evaluare sunt definite in [planul de implementare](docs/IMPLEMENTATION_ROADMAP.md#design-requirement-flexibility-as-capabilities-evolve) si in ADR-007.

## 10. Baseline runtime-v1 si standarde de calitate

Contractul initial implementat, modularitatea si limitele sunt documentate in [RUNTIME_V1_CONTRACT.md](docs/RUNTIME_V1_CONTRACT.md). Runtime-ul include scheme versionate, diagnosticare consistenta si extensia controlata pentru sectiuni. Pipeline-ul de calitate verifica TypeScript strict, ESLint, Prettier, compatibilitatea schemelor generate, teste de contract/securitate si verificari browser/accesibilitate. Acest progres valideaza subsistemul de profiluri; serviciile sociale si fluxul autentificat de publicare raman obligatorii si urmeaza in implementare.

## 11. Fundatia backend implementata

Serviciile identity/profiles folosesc .NET 10, EF Core si baze PostgreSQL separate. Primul flux permite crearea si citirea profilului privat de catre proprietar, cu ICurrentUser decuplat si autentificare falsa exclusiv in proiectul de teste. Migrarile si tranzactia profil/outbox sunt verificate pe PostgreSQL real. Detalii si standarde: [BACKEND_FOUNDATION.md](docs/BACKEND_FOUNDATION.md). Utilizatorul a aprobat OpenIddict si un BFF dedicat ASP.NET Core. Fluxurile aprobate de inregistrare, confirmare email, resetare parola si sesiune BFF sunt implementate. Configurarea si limitele operationale sunt descrise in [AUTHENTICATION.md](docs/AUTHENTICATION.md). Interfata Next.js si publicarea urmeaza in implementare. Plan: [AUTHENTICATION_IMPLEMENTATION_PLAN.md](docs/AUTHENTICATION_IMPLEMENTATION_PLAN.md).

## Deployment local reproductibil

Deployment-ul local aprobat include Caddy HTTPS, Mailpit pentru capturarea emailurilor, baze PostgreSQL separate, migrari explicite si conturi runtime cu privilegii limitate. Cheile Identity/BFF sunt persistente si criptate; scripturile permit pornire, verificare si backup/restaurare in baze temporare. Configurare si limite: [LOCAL_DEPLOYMENT.md](docs/LOCAL_DEPLOYMENT.md). Gazduirea publica si interfata Next.js raman etape separate.
