package npanday.resolver.resolvers;

import java.util.List;
import java.util.Set;

import npanday.resolver.NPandayResolutionCache;
import org.apache.maven.artifact.Artifact;
import org.apache.maven.artifact.factory.ArtifactFactory;
import org.apache.maven.artifact.repository.ArtifactRepository;
import org.apache.maven.artifact.resolver.ArtifactNotFoundException;
import org.apache.maven.artifact.resolver.ArtifactResolutionException;
import org.apache.maven.artifact.resolver.ArtifactResolver;
import org.apache.maven.artifact.resolver.filter.ArtifactFilter;
import org.codehaus.plexus.logging.AbstractLogEnabled;

import npanday.ArtifactType;
import npanday.ArtifactTypeHelper;
import npanday.resolver.ArtifactResolvingContributor;

/**
 * PDB assemblies contributor class.
 * @plexus.component role="npanday.resolver.ArtifactResolvingContributor" role-hint="vsdocs"
 */
public class VsDocsResolver extends AbstractLogEnabled implements ArtifactResolvingContributor 
{
    /** @plexus.requirement */
    ArtifactResolver mavenResolver;

    /** @plexus.requirement */
    ArtifactFactory artifactFactory;

    /** @plexus.requirement */
    NPandayResolutionCache cache;

    /**
     * {@inheritDoc}
     */
    public void tryResolve(Artifact artifact, Set<Artifact> additionalDependenciesCollector, ArtifactFilter filter)
            throws ArtifactNotFoundException
    {
        // NO-OP
    }

    
    /**
     * {@inheritDoc}
     */
    public void contribute(Artifact artifact, ArtifactRepository localRepository,
                           @SuppressWarnings("rawtypes") List remoteRepositories,
                           Set<Artifact> additionalDependenciesCollector, ArtifactFilter filter) throws ArtifactNotFoundException
    {
        final ArtifactType artifactType = ArtifactType.getArtifactTypeForPackagingName(artifact.getType());
        if ( !ArtifactTypeHelper.isDotnetAnyGac(artifactType) && 
             !ArtifactTypeHelper.isComReference(artifactType) &&
             ArtifactTypeHelper.isDotnetLibraryOrExecutable(artifactType))
        {
            tryResolveArtifactVsDocs(artifact, localRepository, remoteRepositories, 
                    additionalDependenciesCollector, filter);
        }
    }

    /**
     * Try to resolve vsdocs documentation file for given artifact.
     *
     * @param artifact
     * @param localRepository
     * @param remoteRepositories
     * @param additionalDependenciesCollector
     * @param filter
     * @throws ArtifactNotFoundException
     */
    private void tryResolveArtifactVsDocs(Artifact artifact, ArtifactRepository localRepository,
                                               List remoteRepositories, Set<Artifact> additionalDependenciesCollector, ArtifactFilter filter) throws ArtifactNotFoundException
    {
        Artifact vsDocsArtifact = artifactFactory.createArtifactWithClassifier(artifact.getGroupId(), artifact.getArtifactId(), 
                artifact.getVersion(), ArtifactType.DOTNET_VSDOCS.getPackagingType(), artifact.getClassifier());
        vsDocsArtifact.setScope(artifact.getScope());
        vsDocsArtifact.setRelease(artifact.isRelease());
        vsDocsArtifact.setDependencyTrail(artifact.getDependencyTrail());

        if(filter != null && !filter.include(vsDocsArtifact)){
            getLogger().debug("NPANDAY-160-003: resolving vsDocs for " + artifact.getId() + " was excluded by a filter");
            return;
        }

        Boolean wasCached = cache.applyTo(vsDocsArtifact);
        if (!wasCached) {
            try {
                mavenResolver.resolve(vsDocsArtifact, remoteRepositories, localRepository);
                // the complimentary artifact should have the same scope as the leading one
                getLogger().debug("NPANDAY-160-001: found a vsDocs for " + artifact.getId());

            } catch (ArtifactNotFoundException e) {
                getLogger().debug("NPANDAY-160-002: no vsDocs found for " + artifact.getId());
            } catch (ArtifactResolutionException e) {
                throw new ArtifactNotFoundException(e.getMessage(), artifact);
            }

            cache.put(vsDocsArtifact);
        }
        if (vsDocsArtifact.isResolved()){
            additionalDependenciesCollector.add(vsDocsArtifact);
        }
    }
}
