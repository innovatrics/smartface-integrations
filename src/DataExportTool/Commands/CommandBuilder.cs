using System.CommandLine;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Commands
{
    public class CommandBuilder
    {
        public Command BuildCommand()
        {
            var rootCommand = new RootCommand
            {
                Description = "SmartFace Export"
            };

            rootCommand.Add(new ExportIndividualsCommand());
            rootCommand.Add(new ExportFacesCommand());

            return rootCommand;
        }
    }
}