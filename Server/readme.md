The server is the heart of the map. It...

1. combines map shards into a format useable by the viewer.
2. generates metadata for points and entities of interest.
3. hosts the web viewer application.

Each connected client sends the server the shards it produces.
To track chunks a DB stores which chunk shards it has, and a hash of the texture.

After that, the server combines them.

To generate metadata, the server scans for P/EOIs each time a chunk is
generated.
These are also added to DBs. Additionally, it tracks when P/EOIs are removed.

Note on nomenclature:

A "shard" is an instance of `ChunkShard` or its texture.
It is the smallest unit of the map and corresponds to a single chunk.
A "tile" is an actual image that is part of the map.
It can be made of any number of chunks.