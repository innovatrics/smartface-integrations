# pip install py-graphql-client
# https://pypi.org/project/py-graphql-client/

from graphql_client import GraphQLClient

def callback(_id, data):
  print(f"msg id: {_id}. data: {data}")

query = """
 subscription {
  matchResult {
    id
    watchlistMemberFullName
    watchlistMemberDisplayName
    watchlistFullName
    watchlistDisplayName
    previewColor
    
    cropImage
    spoofCheck {
      performed
    }
    faceSize
    streamId
    createdAt
  }
}

"""

# Create a list to store subscription IDs
subscription_ids = []

with GraphQLClient('ws://localhost:8097/graphql') as client:

    sub_id = client.subscribe(query, callback=callback)
    subscription_ids.append(sub_id)
    
    try:
        # Keep the script running
        while True:
            pass

    except KeyboardInterrupt:
        # Stop the subscriptions and gracefully exit on Ctrl+C
        for sub_id in subscription_ids:
            client.stop_subscribe(sub_id)