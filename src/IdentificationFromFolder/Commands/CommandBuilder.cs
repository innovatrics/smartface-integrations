using System.CommandLine;

namespace Innovatrics.SmartFace.Integrations.IdentificationFromFolder.Commands
{
    public class CommandBuilder
    {
        public Command BuildCommand()
        {
            var rootCommand = new RootCommand
            {
                Description = "SmartFace Identification"
            };

            rootCommand.Add(new FolderIdentificationCommand());

            return rootCommand;
        }
    }
}