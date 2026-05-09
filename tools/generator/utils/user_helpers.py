import random

from scipy.stats import beta as beta_dist

from utils.faker_instance import fake

# Mixture-of-Beta clustering parameters. Each trait of the user characteristics vector is sampled from
# one of three Beta distributions chosen by a uniform draw, which produces a multimodal distribution
# instead of the unimodal blob that the previous single-Beta-per-trait setup created. Around 30 percent
# of users land in a low cluster, 40 percent in a balanced cluster and 30 percent in a high cluster,
# which gives the NCF embedding layer real subpopulations to separate rather than tiny offsets around
# the global mean. The same scheme is reused for tolerance so a user's pickiness also splits into
# three subpopulations of picky, balanced and flexible eaters, scaled into the historical 0.1-0.7 band
# that downstream affinity scoring already expects.
_CLUSTER_LOW_PROB = 0.30
_CLUSTER_BALANCED_PROB = 0.70  # cumulative, so balanced occupies [0.30, 0.70)
_TOLERANCE_MIN = 0.1
_TOLERANCE_RANGE = 0.6  # tolerance lives in [0.1, 0.7]

def _sample_with_clusters() -> float:
    r = random.random()
    if r < _CLUSTER_LOW_PROB:
        return float(beta_dist.rvs(2, 8))
    elif r < _CLUSTER_BALANCED_PROB:
        return float(beta_dist.rvs(2.5, 2.5))
    else:
        return float(beta_dist.rvs(8, 2))

def _sample_tolerance_with_clusters() -> float:
    return _TOLERANCE_MIN + _TOLERANCE_RANGE * _sample_with_clusters()

def _trait() -> dict:
    return {
        "value": round(_sample_with_clusters(), 3),
        "tolerance": round(_sample_tolerance_with_clusters(), 3),
    }

def generate_user_characteristics_vector() -> dict:
    return {
        "flavor_sweetness": _trait(),
        "flavor_bitterness": _trait(),
        "flavor_spiciness": _trait(),
        "flavor_umami": _trait(),
        "flavor_sourness": _trait(),
        "flavor_saltiness": _trait(),
        "texture_crispy": _trait(),
        "texture_creamy": _trait(),
        "texture_chewy": _trait(),
        "physics_richness": _trait(),
        "physics_temperature": _trait(),
        "physics_freshness": _trait(),
        "context_price_sensitivity": _trait(),
        "context_portion_preference": _trait(),
    }

def generate_full_name() -> str:
    return fake.name()

def generate_phone() -> str:
    return f"+48 {random.randint(500, 999)} {random.randint(100, 999)} {random.randint(100, 999)}"
