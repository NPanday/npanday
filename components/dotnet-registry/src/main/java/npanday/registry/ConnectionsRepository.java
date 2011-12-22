package npanday.registry;

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

import org.apache.maven.artifact.factory.ArtifactFactory;
import org.openrdf.repository.RepositoryException;
import org.openrdf.repository.sail.SailRepository;
import org.openrdf.sail.memory.MemoryStore;

import javax.naming.OperationNotSupportedException;
import java.io.File;
import java.net.URL;
import java.util.Collections;
import java.util.HashSet;
import java.util.Hashtable;
import java.util.Set;
import java.util.logging.Logger;

/**
 * Repository for obtaining sets of DAOs.
 * @plexus.component
 *   role="npanday.registry.ConnectionsRepository"
 */
public class ConnectionsRepository
    implements Repository
{

    private static Logger logger = Logger.getAnonymousLogger();

    private Set<DataAccessObject> daos = new HashSet<DataAccessObject>();

    private Hashtable properties;
    
     /**
     * The artifact factory component, which is used for creating artifacts.
     */
    private ArtifactFactory artifactFactory;

    /**
     * Loads all the data access objects. This is considered a lazy load because the framework (plexus) invoke the
     * ConnectionsRepository.load method, which in this case does nothing more than set the properties. It is up to the
     * developer to explicitly call the lazy load method. This is done for performance reasons.
     *
     * @throws NPandayRepositoryException if there is a problem loading the DAOs
     */
    public void lazyLoad() throws NPandayRepositoryException
    {
       long start = System.currentTimeMillis();

        File dataDir = new File( System.getProperty( "user.home" ), ".npanday" );
        MemoryStore store = new MemoryStore( dataDir );
        store.setPersist( true );
        store.setSyncDelay( 0 );
        org.openrdf.repository.Repository rdfRepository = new SailRepository( store );
        try
        {
            rdfRepository.initialize();
        }
        catch ( RepositoryException e )
        {
            throw new NPandayRepositoryException("NPANDAY-080-005: Failed to initialized repository. Message = " + e.getMessage(), e );
        }

        Set<String> keys = properties.keySet();
        for ( String keyName : keys )
        {
            if ( keyName.trim().startsWith( "dao:" ) )
            {
                String daoClassName = (String) properties.get( keyName );
                try
                {
                    Class c = Class.forName( daoClassName );
                    Object o = c.getConstructor().newInstance();
                    if ( !( o instanceof DataAccessObject ) )
                    {
                        throw new NPandayRepositoryException(
                            "NPANDAY-080-000: dao tag references a class that does not implement the DataAccessObject interface." );
                    }
                    DataAccessObject dao = (DataAccessObject) o;
                    
                    dao.init( rdfRepository, keyName, daoClassName );
                    daos.add( dao );
                    logger.finest( "NPANDAY-080-001: Adding data access object: Class Name = " + daoClassName );
                }
                catch ( Exception e )
                {
                    throw new NPandayRepositoryException(
                        "NPANDAY-080-002: Problem instantiating the DAO Class: Class Name = " + daoClassName, e );
                }
                catch ( Error e )
                {
                    throw new NPandayRepositoryException(
                        "NPANDAY-080-003: Problem instantiating the DAO Class: Class Name = " + daoClassName, e );
                }
            }
        }
        logger.finest( "NPANDAY-080-004: Connection Start Up: Time = " + ( System.currentTimeMillis() - start ) );
    }

    public void load( URL source )
        throws NPandayRepositoryException
    {

    }

    public void clearAll()
        throws OperationNotSupportedException
    {
        throw new OperationNotSupportedException(  );
    }

    /**
     * @see Repository#reloadAll()
     */
    public void reloadAll()
        throws OperationNotSupportedException
    {
        throw new OperationNotSupportedException(  );
    }

    /**
     * The properties configured in the registry.
     */
    public void setProperties( Hashtable props )
    {
        properties = props;
    }

    /**
     * Returns unmodifiable set of all data access objects.
     *
     * @return unmodifiable set of all data access objects
     */
    public Set<DataAccessObject> getDataAccessObjects()
    {
        return Collections.unmodifiableSet( daos );
    }
}
