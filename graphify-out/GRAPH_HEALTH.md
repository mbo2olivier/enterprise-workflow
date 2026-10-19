[graphify] MultiDiGraph edge-collapse diagnostic
input: <in-memory>
input_stage: provided JSON (normal graph.json is post-build)
effective_directed: <direct-call>
nodes: 7792
unverified_code_nodes: 0
raw_edges: 14853
valid_candidate_edges: 14638
missing_endpoint_edges: 0
dangling_endpoint_edges: 209
external_reference_edges: 6
self_loop_edges: 6
exact_duplicate_edges: 10
directed_unique_endpoint_pairs: 13735
directed_same_endpoint_collapsed_edges: 903
undirected_unique_endpoint_pairs: 13690
undirected_same_endpoint_collapsed_edges: 948
same_endpoint_group_count: 481
relation_variant_groups: 18
source_file_variant_groups: 0
source_location_variant_groups: 409
context_variant_groups: 69
post_build_graph_type: Graph
post_build_edges: 13904
producer_suppression_sites: 12
producer_suppression_examples:
  - L1789 seen_ids arity=unknown
  - L2353 seen_ids arity=unknown
  - L2355 seen_doc_refs arity=unknown
  - L3022 seen_ids arity=unknown
  - L3169 seen_ids arity=unknown
  - L4179 seen_keys arity=unknown
  - L4365 seen_keys arity=unknown
  - L7608 seen_ids arity=unknown
examples:
  - src_enterpriseworkflow_web_components_pages_admin -> src_enterpriseworkflow_web_components_pages_admin_razor_phosphoricon edges=13 relations=['calls'] locations=['L13', 'L16', 'L17', 'L18', 'L19', 'L27', 'L31', 'L36'] contexts=['']
  - src_enterpriseworkflow_persistence_oracle_oracleworkflowdbcontext_enterpriseworkflow_persistence_oracle_oracleworkflowdbcontext -> dbset edges=12 relations=['references'] locations=['L10', 'L11', 'L12', 'L13', 'L15', 'L16', 'L17', 'L18', 'L19', 'L20', 'L8', 'L9'] contexts=['field']
  - src_enterpriseworkflow_persistence_sqlite_sqliteworkflowdbcontext_enterpriseworkflow_persistence_sqlite_sqliteworkflowdbcontext -> dbset edges=12 relations=['references'] locations=['L10', 'L12', 'L14', 'L16', 'L18', 'L20', 'L22', 'L23', 'L24', 'L25', 'L26', 'L8'] contexts=['field']
  - src_enterpriseworkflow_runtime_workflowruntimeoptions_enterpriseworkflow_runtime_workflowruntimeoptions -> timespan edges=11 relations=['references'] locations=['L11', 'L12', 'L13', 'L15', 'L16', 'L18', 'L19', 'L20', 'L7', 'L8', 'L9'] contexts=['field']
  - src_enterpriseworkflow_security_persistence_securitydbcontext_enterpriseworkflow_security_persistence_securitydbcontext -> dbset edges=10 relations=['references'] locations=['L10', 'L11', 'L12', 'L13', 'L14', 'L15', 'L16', 'L7', 'L8', 'L9'] contexts=['field']
note: normal graph.json is post-build; raw producer loss must be measured earlier.