package npanday.executable.execution;

/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

import npanday.PathUtil;
import npanday.executable.ExecutionException;
import org.codehaus.plexus.util.cli.CommandLineException;
import org.codehaus.plexus.util.cli.CommandLineUtils;
import org.codehaus.plexus.util.cli.Commandline;
import org.codehaus.plexus.util.cli.StreamConsumer;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

/**
 * @author Shane Isbell
 * @author <a href="mailto:lcorneliussen@apache.org">Lars Corneliussen</a>
 */
public class PlexusUtilsCommandExecutor
    extends CommandExecutorSkeleton
{

    /**
     * Standard Out
     */
    private StreamConsumer stdOut;

    /**
     * Standard Error
     */
    private ErrorStreamConsumer stdErr;

    /**
     * Process result
     */
    private int result;

    @Override
    public void executeCommand(
        String executable, List<String> commands, File workingDirectory, boolean failsOnErrorOutput ) throws
        ExecutionException
    {
        if ( commands == null )
        {
            commands = new ArrayList<String>();
        }
        stdOut = new StandardStreamConsumer( getLogger() );
        stdErr = new ErrorStreamConsumer( getLogger() );

        Commandline commandline = new CustomPlexusUtilsCommandline();

        // NPANDAY-409
        // On non-Windows platforms, such as Linux, "gmcs" not resolved
        // to gmcs.exe in working directory due to /usr/bin/gmcs
        // but "./gmcs.exe" resolved as desired in working directory
        String osName = System.getProperty( "os.name" );
        if ( !osName.toLowerCase().contains( "win" ) )
        {
            File executableFile = PathUtil.getExecutable( workingDirectory, executable );
            // do not prefix for internal commands, such as mkdir
            if ( executableFile != null && workingDirectory.equals( executableFile.getParentFile() ) )
            {
                executable = new File( "./", executableFile.getName() ).toString();
            }
        }

        commandline.setExecutable( executable );
        commandline.addArguments( commands.toArray( new String[commands.size()] ) );

        if ( workingDirectory != null && workingDirectory.exists() )
        {
            // TODO: Wrong use of working directory! $(basedir) should be the working dir,
            // and the executable paths should be absolute
            commandline.setWorkingDirectory( workingDirectory.getAbsolutePath() );
        }
        else if ( workingDirectory != null && !workingDirectory.exists() )
        {
            getLogger().info(
                "NPANDAY-040-006: Did not find executable path for " + executable + ", " + "will try system path"
            );
        }

        try
        {
            result = CommandLineUtils.executeCommandLine( commandline, stdOut, stdErr );
            getLogger().debug(
                "NPANDAY-040-005: Executed command: Commandline = " + commandline + ", Result = " + result
            );

            if ( ( failsOnErrorOutput && stdErr.hasError() ) || result != 0 )
            {
                throw new ExecutionException(
                    "NPANDAY-040-001: Could not execute: Command = " + commandline.toString() + ", Result = " + result
                );
            }
        }
        catch ( CommandLineException e )
        {
            throw new ExecutionException(
                "NPANDAY-040-002: Could not execute: Command = " + commandline.toString(), e
            );
        }
    }

    public int getResult()
    {
        return result;
    }

    public String getStandardOut()
    {
        return stdOut.toString();
    }

    public String getStandardError()
    {
        return stdErr.toString();
    }

}
