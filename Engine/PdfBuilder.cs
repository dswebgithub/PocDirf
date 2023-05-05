using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PocDirf.Model;
using System.IO;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Text.RegularExpressions;
using iTextSharp.text;
using System.ComponentModel;
using System.Threading;
using System.Xml.Serialization;

namespace PocDirf.Engine
{

    public class PdfBuilderSettings
    {
        public string PdfFileInputPattern { get; set; }
        public bool AnalyzeSubFolders { get; set; }
        public string InputFolder { get; set; }
        public string OutputFolder { get; set; }
        public string ErrorFolder { get; set; }

        public string ReferencePath { get; set; }
    }

    public class BuildProgressEventArgs : EventArgs
    {
        private int _progress;
        private string _message;

        public int Progress
        {
            get { return _progress; }
        }

        public string Message
        {
            get { return _message; }
        }

        public BuildProgressEventArgs(int progress, string message)
        {
            _progress = progress;
            _message = message;
        }

    }

    public class BuildCompletedEventArgs : EventArgs
    {
        private object _state;
        private bool _canceled;
        private Exception _error;

        public object State
        {
            get { return _state; }
        }

        public bool Cancelled
        {
            get { return _canceled; }
        }

        public Exception Error
        {
            get { return _error; }
        }

        public BuildCompletedEventArgs(object state, bool cancelled, Exception error)
        {
            _state = state;
            _canceled = cancelled;
            _error = error;
        }

    }

    public class PdfBuilder
    {
        //---------------------
        public int TotalRegiteredsColaborators
        {
            get
            {
                if (_normalizedColaborators == null)
                    return 0;
                return _normalizedColaborators.Count;
            }
        }

        public int TotalDirfColaborators
        {
            get
            {
                if (_pdfData == null)
                    return 0;
                return _pdfData.Where(x => x.Value.PdfType == PdfType.Dirf).Count();
            }
        }

        public int TotalMedicalDetailsColaborators
        {
            get
            {
                if (_pdfData == null)
                    return 0;
                return _pdfData.Where(x => x.Value.PdfType == PdfType.MedicalDetail).Count();
            }
        }



        //---------------------

        private Regex _cpfRegex = new Regex(@"[0-9][0-9][0-9]\.[0-9][0-9][0-9]\.[0-9][0-9][0-9]-[0-9][0-9]");
        private Regex _cpfRegex2 = new Regex(@"\nBeneficiário: [0-9][0-9][0-9]\.[0-9][0-9][0-9]\.[0-9][0-9][0-9]-[0-9][0-9]");
        private Regex _cnpjRegex = new Regex(@"[0-9][0-9]\.[0-9][0-9][0-9]\.[0-9][0-9][0-9]/[0-9][0-9][0-9][0-9]-[0-9][0-9]");
        private Regex _functionalIDRegex = new Regex(@"\n[0-9]{8}\n");


        private Regex _cpfNumbersOnlyRegex = new Regex(@"CPF[ ]*\n*[0-9]{11}");
        //private Regex _cpfNumbersOnlyRegex = new Regex(@"C.P.F.:[ ]*\n*[0-9]{11}");
        private Regex _cnpjNumbersOnlyRegex = new Regex(@"[0-9][0-9]\.[0-9][0-9][0-9]\.[0-9][0-9][0-9]/[0-9][0-9][0-9][0-9]-[0-9][0-9]");



        //teste mineradora: arquivo  dirf do governo
        private Regex _dirfRegex = new Regex(@"MINISTÉRIO DA ECONOMIA\nComprovante de Rendimentos Pagos e de\nSecretaria Especial da Receita Federal do Brasil\nImposto sobre a Renda Retido na Fonte\nImposto sobre a Renda da Pessoa Física\nExercício de 2023 Ano-calendário de 2022\nVerifique as condições e o prazo para a apresentação da Declaração do Imposto sobre a Renda da Pessoa Física para este ano-calendário no sítio\nda Secretaria Especial da Receita Federal do Brasil na Internet, no endereço <https://www.gov.br/receitafederal/pt-br>.");
        private Regex _dirfRegex2 = new Regex(@"Aprovado pela Instrução Normativa RFB nº 2.060, de 13 de dezembro de 2021.\nPág. 2");

        //correto
        //private Regex _dirfRegex = new Regex(@"COMPROVANTE DE RENDIMENTOS PAGOS\nE DE RETENÇÃO DE IMPOSTO DE RENDA NA FONTE\nAno Calendário: 2022");
        //private Regex _dirfRegex2 = new Regex(@"Aprovado pela Instrução Normativa RFB nº 2.060, de 13 de dezembro de 2021.");
        private Regex _medicalDetailsRegex2 = new Regex(@"^ Demonstrativo de Desconto Referente a Plano de Saúde \n Ano Calendário  2022");
        private Regex _medicalDetailsRegex = new Regex(@"Ministério da Fazenda Comprovante de Rendimentos Pagos e de\nSecretaria da Receita Federal do Brasil\nImposto sobre a Renda Retido na Fonte\nImposto sobre a Renda da Pessoa Física\nAno-calendário de 2022\nExercício de 2023");


        private Dictionary<string, PdfData> _pdfData;
        private Dictionary<string, Colaborator> _normalizedColaborators = new Dictionary<string, Colaborator>();
        private Dictionary<string, Company> _companies = new Dictionary<string, Company>();
        private Dictionary<string, Subsidiary> _subsidiaries = new Dictionary<string, Subsidiary>();

        private Dictionary<string, string> _outputFiles = new Dictionary<string, string>();

        private ColaboratorDataCollection _colaborators;
        private PdfBuilderSettings _settings;

        private BackgroundWorker _worker;
        private SynchronizationContext _context;

        //private const string _csvDataFileName = "ColaboradorJBS.csv";
        //private const string _csvDataFileName = "ColaboradorSeara.csv";
        private const string _csvDataFileName = "ColaboradorCol01.csv";
        //"CorrecaoPlanoMedico_Izabel_ColaboratorsRM.csv"; //"ColaboratorsRM.csv";

        public event EventHandler<BuildProgressEventArgs> BuildProgress;
        private void FireProgress(int progress, string message)
        {
            if (BuildProgress != null)
            {
                BuildProgress(this, new BuildProgressEventArgs(progress, message));
            }
        }


        public event EventHandler<BuildCompletedEventArgs> BuildCompleted;
        private void FireCompleted(object state, bool cancelled, Exception error)
        {
            if (BuildCompleted != null)
            {
                BuildCompleted(this, new BuildCompletedEventArgs(state, cancelled, error));
            }
        }

        public PdfBuilderSettings Settings
        {
            get { return _settings; }
            set { _settings = value; }
        }

        public PdfBuilder(PdfBuilderSettings settings)
        {
            _settings = settings;
            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += _worker_DoWork;
            _worker.RunWorkerCompleted += _worker_RunWorkerCompleted;
            _worker.ProgressChanged += _worker_ProgressChanged;

            _context = SynchronizationContext.Current;
        }

        private void _worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            FireProgress(e.ProgressPercentage, (string)e.UserState);
        }

        private void _worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            FireCompleted(e.Result, e.Cancelled, e.Error);
        }

        private void LoadPdfs(string pattern, string directory, bool analizeSubdirectories)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(PdfData));

            if (!Directory.Exists(directory))
                throw new IOException("O diretório de entrada informado não existe.");

            if (_pdfData == null)
                _pdfData = new Dictionary<string, PdfData>();
            else
                _pdfData.Clear();

            var files = Directory.GetFiles(directory, pattern, analizeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            var orderedFiles = new List<string>(files);
            orderedFiles.Sort();
            files = orderedFiles.ToArray();

            var fileCounter = 0;
            var buildCounter = 0;

            //teste 04/03/2015
            string cpf = string.Empty;
            string cnpj = string.Empty;
            string functionalID = string.Empty;

            foreach (var file in files)
            {
                PdfReader reader = new PdfReader(file);

                if (reader.IsRebuilt())
                {
                    Log(string.Format("O arquivo '{0}' não é um PDF válido", file));
                }
                else
                {
                    int numPages = reader.NumberOfPages;
                    fileCounter++;


                    for (int i = 1; i <= numPages; i++)
                    {
                        if (_worker.CancellationPending)
                        {
                            reader.Close();
                            goto cancel;
                        }


                        buildCounter++;
                        _worker.ReportProgress((int)((i / (float)numPages) * 100), string.Format("{3:0000000#} - Gerando informações de '{0}' ({1} de {2} arquivos origem)", file, fileCounter, files.Length, buildCounter));




                        if (i > 0)
                        //if (i > 54439  && i < 54450) 
                        {
                            string textContent = PdfTextExtractor.GetTextFromPage(reader, i);

                            int v_pagina = 0;

                            PdfType pdfType = PdfType.Unknow;

                            if (_dirfRegex.IsMatch(textContent))
                                pdfType = PdfType.Dirf;
                            else if (_dirfRegex2.IsMatch(textContent))
                            {
                                pdfType = PdfType.Dirf;
                                v_pagina = 1;
                            }
                            else if (_medicalDetailsRegex.IsMatch(textContent))
                                pdfType = PdfType.MedicalDetail;
                            else if (_medicalDetailsRegex2.IsMatch(textContent))
                                pdfType = PdfType.MedicalDetail;


                            if (pdfType == PdfType.Unknow)
                                continue;

                            //string cpf = string.Empty;

                            //string cnpj = string.Empty;

                            //string functionalID = string.Empty;

                            if (v_pagina == 0)
                            {
                                if (pdfType == PdfType.MedicalDetail)
                                {
                                    cpf = _cpfNumbersOnlyRegex.Match(textContent).Value;
                                    cnpj = _cnpjRegex.Match(textContent).Value;

                                    functionalID = _functionalIDRegex.Match(textContent).Value;
                                }
                                else if (pdfType == PdfType.Dirf)
                                {

                                    //cpf = _cpfRegex.Match(textContent).Value.ToString();

                                    //if (_cpfRegex.Match(textContent).Value.ToString() == "000.000.000-00")
                                    //{
                                    //    cpf = _cpfRegex2.Match(textContent).Value;
                                    //}
                                    //else
                                    //{
                                    //    cpf = _cpfRegex.Match(textContent).Value;
                                    //}

                                    if (_cpfRegex.Match(textContent).Value != "")
                                    {
                                        cpf = _cpfRegex.Match(textContent).Value;
                                        cnpj = _cnpjRegex.Match(textContent).Value;
                                    }

                                }

                                cpf = cpf.Replace(".", "")
                                            .Replace("-", "")
                                            .Replace("CPF", "")
                                            .Replace("\nBeneficiário:", "")
                                            .Replace("\n", "")
                                            .Replace(" ", "")
                                            .PadLeft(11, '0');

                                cnpj = cnpj.Replace(".", "")
                                            .Replace("-", "")
                                            .Replace("/", "")
                                            .PadLeft(14, '0');


                                functionalID = functionalID.Replace(".", "")
                                            .Replace("-", "")
                                            .Replace("Matrícula:", "")
                                            .Replace("\n", "")
                                            .Replace(" ", "")
                                            .PadLeft(0, '0');

                            }

                            var pdfData = new PdfData
                            {
                                ColaboratorID = cpf,
                                CompanyID = cnpj,
                                ColaboratorFunctionalID = functionalID,
                                InitialPage = i,
                                NumberOfPages = 1, //HACK: forçado
                                File = file,
                                PdfType = pdfType
                            };

                            //teste
                            //if (v_pagina == 1)
                            //{
                            //    _pdfData.Add(pdfData.FunctionalUniqueID, pdfData);
                            //}
                            //else
                            //{
                            if (pdfData.PdfType == PdfType.Dirf && !_pdfData.ContainsKey(pdfData.UniqueID))
                            {
                                _pdfData.Add(pdfData.UniqueID, pdfData);
                            }
                            else if (pdfData.PdfType == PdfType.MedicalDetail && !_pdfData.ContainsKey(pdfData.FunctionalUniqueID))
                            {
                                _pdfData.Add(pdfData.FunctionalUniqueID, pdfData);
                            }
                            //else
                            //{
                            //    var errorFileName = Path.Combine(_settings.ErrorFolder, string.Format("pdf_{0}_{1}.xml", pdfData.UniqueID, buildCounter));
                            //    using (FileStream errorStream = new FileStream(errorFileName, FileMode.Create, FileAccess.Write))
                            //    {
                            //        pdfData.Message = "Erro ao adicional na coleção '_pdfData'. Chave já existe.";
                            //        serializer.Serialize(errorStream, pdfData);
                            //    }
                            //    continue;
                            //}
                            //}

                            if (_normalizedColaborators.ContainsKey(pdfData.ColaboratorID))
                            {
                                var colaborator = _normalizedColaborators[pdfData.ColaboratorID];
                                if (colaborator.Pdfs == null)
                                    colaborator.Pdfs = new List<PdfData>();
                                colaborator.Pdfs.Add(pdfData);

                                string companyPath;
                                string subsidiaryPath;
                                string fileName;

                                BuildPaths(colaborator, Settings.OutputFolder, out companyPath, out subsidiaryPath, out fileName);

                                if (!Directory.Exists(Settings.OutputFolder))
                                    Directory.CreateDirectory(Settings.OutputFolder);
                                if (!Directory.Exists(companyPath))
                                    Directory.CreateDirectory(companyPath);
                                if (!Directory.Exists(subsidiaryPath))
                                    Directory.CreateDirectory(subsidiaryPath);

                                pdfData.OutputFile = fileName;
                                var pdfExists = _outputFiles.ContainsKey(fileName);
                                if (!pdfExists)
                                    _outputFiles.Add(fileName, string.Empty);

                                bool appendPage = File.Exists(fileName) || pdfExists;

                                using (var ms = new MemoryStream())
                                {
                                    if (appendPage)
                                    {
                                        bool loadFail;
                                        do
                                        {
                                            var load = File.ReadAllBytes(fileName);
                                            ms.Write(load, 0, load.Length);
                                            ms.Position = 0;
                                            if (load.Length <= 0)
                                            {
                                                loadFail = true;
                                                Thread.Sleep(500);
                                            }
                                            else
                                                loadFail = false;
                                        }
                                        while (loadFail);

                                    }
                                    ms.Position = 0;

                                    using (var outPutFileStream = new FileStream(fileName, FileMode.Create))
                                    {




                                        Document document = new Document();
                                        PdfCopy copy = new PdfCopy(document, outPutFileStream);
                                        document.Open();

                                        // copiar páginas existentes
                                        if (appendPage && ms.Length > 0)
                                        {
                                            PdfReader reader2 = new PdfReader(ms);
                                            if (!reader2.IsRebuilt())
                                            {
                                                for (int j = 1; j <= reader2.NumberOfPages; j++)
                                                {
                                                    string textContentTemp = PdfTextExtractor.GetTextFromPage(reader2, j);


                                                    //////////////////////////////////////
                                                    bool forceCopy = true;
                                                    // Não duplicar informes médicos
                                                    //////////////////////////////////////
                                                    if (_medicalDetailsRegex.IsMatch(textContentTemp) || _medicalDetailsRegex2.IsMatch(textContentTemp))
                                                    {
                                                        var cpfTemp = _cpfNumbersOnlyRegex.Match(textContentTemp).Value;
                                                        var cnpjTemp = _cnpjRegex.Match(textContentTemp).Value;
                                                        var functionalIDTemp = _functionalIDRegex.Match(textContentTemp).Value;

                                                        cpfTemp = cpfTemp.Replace(".", "")
                                                                   .Replace("-", "")
                                                                   .Replace("CPF:", "")
                                                                   .Replace("\n", "")
                                                                   .Replace(" ", "")
                                                                   .PadLeft(11, '0');

                                                        cnpjTemp = cnpjTemp.Replace(".", "")
                                                                    .Replace("-", "")
                                                                    .Replace("/", "")
                                                                    .PadLeft(14, '0');

                                                        functionalIDTemp = functionalIDTemp.Replace(".", "")
                                                                    .Replace("-", "")
                                                                    .Replace("Matrícula:", "")
                                                                    .Replace("\n", "")
                                                                    .Replace(" ", "")
                                                                    .PadLeft(0, '0');

                                                        var comparerEquals = string.Compare(cpf, cpfTemp) == 0 &&
                                                                        string.Compare(cnpj, cnpjTemp) == 0 &&
                                                                        string.Compare(functionalID, functionalIDTemp) == 0;
                                                        if (comparerEquals)
                                                        {
                                                            forceCopy = false;
                                                        }
                                                        else
                                                        {
                                                            //LogTemp(fileName);
                                                        }
                                                    }

                                                    if (forceCopy)
                                                    {
                                                        document.NewPage();
                                                        var copyPage = copy.GetImportedPage(reader2, j);
                                                        copy.AddPage(copyPage);
                                                    }
                                                    else
                                                    {
                                                        ;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                ;
                                            }
                                        }
                                        else
                                        {
                                            ;
                                        }

                                        copy.AddPage(copy.GetImportedPage(reader, i));
                                        document.Close();
                                        LogTemp(fileName);

                                    }
                                }

                            }
                        }//fim if teste
                    }
                }

                reader.Close();

            }

            cancel:
            ;

        }



        private void LogTemp(string lineData)
        {
            using (var fileTempLog = new FileStream("c:\\temp\\dirfLog2.txt", FileMode.Append, FileAccess.Write))
            {
                using (var writer = new StreamWriter(fileTempLog))
                {
                    writer.AutoFlush = true;
                    writer.WriteLine(lineData);
                }
            }



        }

        public static void BuildPaths(Colaborator colaborator, string referenceFolder, out string companyPath, out string subsidiaryPath, out string fileName)
        {
            companyPath = Path.Combine(referenceFolder, PathEncode(colaborator.CurrentSubsidiary.ParentCompany.Name) + "\\");
            subsidiaryPath = Path.Combine(companyPath, PathEncode(colaborator.CurrentSubsidiary.Name) + "\\");
            var splitName = colaborator.Name.Split(' ');
            var firstName = splitName[0];
            var lastName = splitName[splitName.Length - 1];
            fileName = Path.Combine(subsidiaryPath, string.Format("Informe_{0}_{1}_{2}.pdf", colaborator.ID, firstName, lastName));
        }

        private static string PathEncode(string text)
        {
            var invalids = Path.GetInvalidPathChars();
            foreach (char invalid in invalids)
                text = text.Replace(invalid.ToString(), "");
            return text.Replace("/", "").Replace("\\", "").Replace(".", "");
        }

        private void Log(string p)
        {
            //throw new NotImplementedException();
        }

        public void PreLoad()
        {
            PreLoad(null);
        }

        public void PreLoad(PdfBuilderSettings settings)
        {
            if (_settings == null && settings == null)
                throw new Exception("A propriedade 'settings' está nula.");

            if (settings != null)
                _settings = settings;

            if (_colaborators == null)
            {
                FileInfo info = new FileInfo(settings.ReferencePath);
                var csvColabotatorsRM = Path.Combine(Path.Combine(info.DirectoryName, "Data\\"), _csvDataFileName);
                LoadColaborators(csvColabotatorsRM);
            }
        }


        public static Dictionary<string, Colaborator> BuildColaborators(ColaboratorData[] colaborators)
        {
            Dictionary<string, Company> companies = new Dictionary<string, Company>();
            Dictionary<string, Subsidiary> subsidiaries = new Dictionary<string, Subsidiary>();
            Dictionary<string, Colaborator> normalizedColaborators = new Dictionary<string, Colaborator>();

            int counter = 0;
            foreach (var colaborator in colaborators)
            {
                counter++;

                colaborator.ID = colaborator.ID
                                .Replace(".", "")
                                .Replace("-", "")
                                .PadLeft(11, '0');
                colaborator.SubsidiaryID = colaborator.SubsidiaryID
                                .Replace(".", "")
                                .Replace("-", "")
                                .Replace("/", "")
                                .PadLeft(14, '0');
                colaborator.CampanyID = colaborator.CampanyID
                                .Replace(".", "")
                                .Replace("-", "")
                                .Replace("/", "")
                                .PadLeft(14, '0');
                colaborator.FunctionalID = colaborator.FunctionalID
                                .Replace(".", "")
                                .Replace("-", "")
                                .Replace("/", "")
                                .PadLeft(8, '0');

                if (!companies.ContainsKey(colaborator.CampanyID))
                {
                    companies.Add(colaborator.CampanyID,
                                    new Company
                                    {
                                        ID = colaborator.CampanyID,
                                        LegalName = colaborator.CompanyName,
                                        Name = colaborator.CompanyName,
                                        Subsidiaries = new List<Subsidiary>()
                                    });
                }
                var company = companies[colaborator.CampanyID];


                if (!subsidiaries.ContainsKey(colaborator.SubsidiaryID))
                {
                    subsidiaries.Add(colaborator.SubsidiaryID,
                                    new Subsidiary
                                    {
                                        ID = colaborator.SubsidiaryID,
                                        LegalName = colaborator.SubsidiaryName,
                                        Name = colaborator.SubsidiaryName,
                                        ParentCompany = company
                                    });

                    company.Subsidiaries.Add(subsidiaries[colaborator.SubsidiaryID]);
                }
                var subsidiary = subsidiaries[colaborator.SubsidiaryID];

                if (!normalizedColaborators.ContainsKey(colaborator.ID))
                {
                    normalizedColaborators.Add(colaborator.ID,
                                    new Colaborator
                                    {
                                        ID = colaborator.ID,
                                        FunctionalID = colaborator.FunctionalID,
                                        Name = colaborator.Name,
                                        HireDateSubsidiaries = new Dictionary<InstanceSubsidiaryData, Subsidiary>()
                                    });
                }
                var hireData = new InstanceSubsidiaryData { FunctionalID = colaborator.FunctionalID, HireDate = colaborator.HireDate };
                var normalizedColaborator = normalizedColaborators[colaborator.ID];
                if (!normalizedColaborator.HireDateSubsidiaries.ContainsKey(hireData))
                {
                    normalizedColaborator.HireDateSubsidiaries.Add(hireData, subsidiary);
                }
                else
                {
                    DateTime hireDate = colaborator.HireDate.AddDays(1);
                    hireData.HireDate = hireDate;
                    normalizedColaborator.HireDateSubsidiaries.Add(hireData, subsidiary);
                }

            }

            return normalizedColaborators;
        }

        private void LoadColaborators(string csvFile)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ColaboratorData));

            ColaboratorData[] colaborators = ColaboratorCsvAdapter.ConvertCsvToClassModel(csvFile);
            _normalizedColaborators.Clear();
            _subsidiaries.Clear();
            _companies.Clear();

            int counter = 0;
            foreach (var colaborator in colaborators)
            {
                counter++;

                colaborator.ID = colaborator.ID
                                .Replace(".", "")
                                .Replace("-", "")
                                .PadLeft(11, '0');
                colaborator.SubsidiaryID = colaborator.SubsidiaryID
                                .Replace(".", "")
                                .Replace("-", "")
                                .Replace("/", "")
                                .PadLeft(14, '0');
                colaborator.CampanyID = colaborator.CampanyID
                                .Replace(".", "")
                                .Replace("-", "")
                                .Replace("/", "")
                                .PadLeft(14, '0');

                if (!_companies.ContainsKey(colaborator.CampanyID))
                {
                    _companies.Add(colaborator.CampanyID,
                                    new Company
                                    {
                                        ID = colaborator.CampanyID,
                                        LegalName = colaborator.CompanyName,
                                        Name = colaborator.CompanyName,
                                        Subsidiaries = new List<Subsidiary>()
                                    });
                }
                var company = _companies[colaborator.CampanyID];


                if (!_subsidiaries.ContainsKey(colaborator.SubsidiaryID))
                {
                    _subsidiaries.Add(colaborator.SubsidiaryID,
                                    new Subsidiary
                                    {
                                        ID = colaborator.SubsidiaryID,
                                        LegalName = colaborator.SubsidiaryName,
                                        Name = colaborator.SubsidiaryName,
                                        ParentCompany = company
                                    });

                    company.Subsidiaries.Add(_subsidiaries[colaborator.SubsidiaryID]);
                }
                var subsidiary = _subsidiaries[colaborator.SubsidiaryID];

                if (!_normalizedColaborators.ContainsKey(colaborator.ID))
                {
                    _normalizedColaborators.Add(colaborator.ID,
                                    new Colaborator
                                    {
                                        ID = colaborator.ID,
                                        FunctionalID = colaborator.FunctionalID,
                                        Name = colaborator.Name,
                                        HireDateSubsidiaries = new Dictionary<InstanceSubsidiaryData, Subsidiary>()
                                    });
                }
                var hireData = new InstanceSubsidiaryData { FunctionalID = colaborator.FunctionalID, HireDate = colaborator.HireDate };
                var normalizedColaborator = _normalizedColaborators[colaborator.ID];
                if (!normalizedColaborator.HireDateSubsidiaries.ContainsKey(hireData))
                {
                    normalizedColaborator.HireDateSubsidiaries.Add(hireData, subsidiary);
                }
                else
                {
                    var errorFileName = Path.Combine(_settings.ErrorFolder, string.Format("colaborator_{0}_{1}.xml", colaborator.UniqueID, counter));
                    using (FileStream errorStream = new FileStream(errorFileName, FileMode.Create, FileAccess.Write))
                    {
                        serializer.Serialize(errorStream, colaborator);
                    }
                    DateTime hireDate = colaborator.HireDate.AddDays(1);
                    normalizedColaborator.HireDateSubsidiaries.Add(hireData, subsidiary);

                }

            }

            if (_colaborators != null)
                _colaborators.Clear();
            else
                _colaborators = new ColaboratorDataCollection();

            _colaborators.AddRange(colaborators);
            _colaborators.Sort();
        }

        public List<string> Build()
        {
            if (_worker.IsBusy)
                return new List<string> { "A geração já está em execução." };

            if (_settings == null)
                throw new Exception("A propriedade 'settings' está nula.");

            if (_colaborators == null)
            {
                FileInfo info = new FileInfo(_settings.ReferencePath);
                var csvColabotatorsRM = Path.Combine(Path.Combine(info.DirectoryName, "Data\\"), _csvDataFileName);
                LoadColaborators(csvColabotatorsRM);
            }



            _worker.RunWorkerAsync(_settings);

            return new List<string>();
        }

        private void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            //DeleteOutputContent(_settings);
            LoadPdfs(_settings.PdfFileInputPattern, _settings.InputFolder, _settings.AnalyzeSubFolders);
        }

        private void DeleteOutputContent(PdfBuilderSettings _settings)
        {
            //Directory.Delete(_settings.OutputFolder, true);
            //Directory.CreateDirectory(_settings.OutputFolder);
        }

        private void AssociatePdfToColaborators()
        {
            //foreach (var colaborator in _colaborators)
            //{
            //  colaborator.PdfData = new List<PdfData>();
            //  if(_pdfData.ContainsKey(colaborator.UniqueID))
            //    colaborator.PdfData.Add(_pdfData[colaborator.UniqueID]);
            //}



        }

        public ColaboratorCollection Colaborators
        {
            get
            {
                var result = new ColaboratorCollection();
                result.AddRange(_normalizedColaborators.Values);
                return result;
            }
        }

        public void BuildPdf(string outputDirectory)
        {

        }




        public void Cancel()
        {
            _worker.CancelAsync();
        }

        public bool IsBusy { get { return _worker.IsBusy; } }
    }
}
