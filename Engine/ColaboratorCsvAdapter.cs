using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FileHelpers;
using PocDirf.Model;

namespace PocDirf
{
  public static class ColaboratorCsvAdapter
  {
    public static ColaboratorData[] ConvertCsvToClassModel(string csvPath)
    {
      var engine = new FileHelperEngine<ColaboratorData>();
      engine.ErrorManager.ErrorMode = ErrorMode.ThrowException;

      ColaboratorData[] result = engine.ReadFile(csvPath);

      if (engine.ErrorManager.ErrorCount > 0)
        engine.ErrorManager.SaveErrors("Errors.txt");

      return result;
    }
  }
  
  
 

}
