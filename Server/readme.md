The server is the heart of the map. It...

1. combines map shards into a format useable by the viewer.
2. generates metadata for points and entities of interest.
3. hosts the web viewer application.

Each connected client sends the server the shards it produces.
To track chunks, a DB stores which chunk shards it has, and when they were
generated.

After that, the server combines them.

To generate metadata, the server scans for P/EOIs each time a chunk is
generated.
These are also added to DBs. Additionally, it tracks when P/EOIs are removed.


