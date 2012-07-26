﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tools.Math.AI.Genetic.Operations.CrossOver;
using Tools.Math.AI.Genetic;
using Tools.Math.AI.Genetic.Solvers;
using Tools.Math.Random;
using Tools.Math.VRP.MultiSalesman.Genetic.Helpers;


namespace Tools.Math.VRP.MultiSalesman.Genetic.Operations.CrossOver
{
    internal class RandomRandomBestSelectionCrossOverOperation : ICrossOverOperation<Genome, Problem, Fitness>
    {
        public string Name
        {
            get
            {
                return "NotSet";
            }
        }

        public Individual<Genome, Problem, Fitness> CrossOver(
            Solver<Genome, Problem, Fitness> solver, Individual<Genome, Problem, Fitness> parent1, Individual<Genome, Problem, Fitness> parent2)
        {
            List<Genome> genomes = null;

            Individual new_individual = new Individual();

            List<Genome> parent1_genomes = parent1.Copy().Genomes;
            List<Genome> parent2_genomes = parent2.Copy().Genomes;

            // select the 'best' genomes.
            
            genomes = new List<Genome>();
            while (parent1_genomes.Count > 0 || parent2_genomes.Count > 0)
            {
                // try from parent 1
                Genome genome = null;
                if (parent1_genomes.Count > 0)
                {
                    genome = parent1_genomes[StaticRandomGenerator.Get().Generate(parent1_genomes.Count)];
                    if (!IndividualHelper.Overlaps(genomes, genome))
                    {
                        genomes.Add(genome);
                    }
                    parent1_genomes.Remove(genome);
                }
                if (parent2_genomes.Count > 0)
                {
                    genome = parent2_genomes[StaticRandomGenerator.Get().Generate(parent2_genomes.Count)];
                    if (!IndividualHelper.Overlaps(genomes, genome))
                    {
                        genomes.Add(genome);
                    }
                    parent2_genomes.Remove(genome);
                }
            }

            // list the rest of the cities and divide them into new routes.
            List<int> rest = new List<int>();
            for (int city_to_place = 0; city_to_place < solver.Problem.Cities; city_to_place++)
            {
                if (!IndividualHelper.Overlaps(genomes, city_to_place))
                {
                    rest.Add(city_to_place);
                }
            }

            // create the new individual.
            new_individual.Initialize(genomes);
            new_individual.CalculateFitness(
                solver.Problem,
                solver.FitnessCalculator,
                false);

            // TODO: place the rest of the cities in existing rounds.
            List<int> failed = new List<int>();
            while (rest.Count > 0)
            {
                // select random from current.
                int current_city = rest[StaticRandomGenerator.Get().Generate(
                    rest.Count)];
                rest.Remove(current_city);

                // make a list of potential targets.
                List<Genome> potential_genomes = new List<Genome>();
                for (int round_idx = 0; round_idx < genomes.Count; round_idx++)
                {
                    if (new_individual.Fitness.LargestRoundCategories[round_idx] == 0)
                    {
                        potential_genomes.Add(new_individual.Genomes[round_idx]);
                    }
                }

                // try to place the current city in one of the targets.
                bool succes = false;
                while (potential_genomes.Count > 0)
                {
                    int random_idx = StaticRandomGenerator.Get().Generate(potential_genomes.Count);
                    Genome current = potential_genomes[random_idx];
                    int genome_idx = new_individual.Genomes.IndexOf(current);
                    current = new Genome(current);
                    potential_genomes.RemoveAt(random_idx);

                    Individual<Genome, Problem, Fitness> copy = new_individual.Copy();

                    Tools.Math.VRP.MultiSalesman.Genetic.Helpers.BestPlacementHelper.BestPlacementResult result =
                        BestPlacementHelper.CalculateBestPlacementInGenome(
                            solver.Problem,
                            solver.FitnessCalculator as FitnessCalculator,
                            current,
                            current_city);

                    // do the placement.
                    IndividualHelper.PlaceInGenome(
                        current,
                        result.CityIdx,
                        result.City);

                    // re-calculate fitness
                    copy.Genomes[genome_idx] = current;
                    copy.CalculateFitness(solver.Problem, solver.FitnessCalculator, false);

                    // if the round is still not too big; keep the result.
                    if (copy.Fitness.LargestRoundCategories[genome_idx] == 0)
                    {
                        new_individual = (copy as Individual);
                        succes = true;
                        break;
                    }
                }

                // add to failed if not succesfull
                if (!succes)
                {
                    failed.Add(current_city);
                }
            }
            rest = failed;

            bool new_round = true;
            Genome current_round = null;
            double previous_time = 0;
            while (rest.Count > 0)
            {
                // create a new round if needed.
                int city;
                int city_idx;

                // force placement if less than half of the average round size remains.
                int size = solver.Problem.Cities / new_individual.Genomes.Count;
                if (new_round && rest.Count < size / 2)
                {
                    new_individual = BestPlacementHelper.DoFast(
                        solver.Problem,
                        solver.FitnessCalculator as FitnessCalculator,
                        new_individual,
                        rest);

                    rest.Clear();

                    break;
                }

                if (new_round)
                {
                    new_round = false;
                    current_round = new Genome();
                    new_individual.Genomes.Add(current_round);

                    // select a random city to place.
                    city_idx = StaticRandomGenerator.Get().Generate(rest.Count);
                    city = rest[city_idx];
                    rest.RemoveAt(city_idx);
                    current_round.Add(city);

                    previous_time = solver.Problem.TargetTime.Value;
                }

                if (rest.Count > 0)
                {
                    // find the best city to place next.
                    // calculate the best position to place the next city.
                    BestPlacementHelper.BestPlacementResult new_position_to_place =
                        BestPlacementHelper.CalculateBestPlacementInGenome(
                            solver.Problem,
                            solver.FitnessCalculator as FitnessCalculator,
                            current_round,
                            rest);

                    city = new_position_to_place.City;

                    // remove the node from the source list.
                    rest.Remove(city);

                    // place the node.
                    current_round.Insert(new_position_to_place.CityIdx, new_position_to_place.City);

                    // calculate the time.
                    double time = (solver.FitnessCalculator as FitnessCalculator).CalculateTime(
                        solver.Problem, current_round);

                    if (solver.Problem.TargetTime.Value < time)
                    { // time limit has been reached.
                        double diff_average = time - solver.Problem.TargetTime.Value;
                        double diff_previous = solver.Problem.TargetTime.Value - previous_time;

                        if (diff_average > diff_previous)
                        { // remove the last added city.
                            current_round.Remove(city);
                            rest.Add(city);
                        }
                        else
                        { // keep the last city.

                        }

                        // keep the generated round.
                        new_round = true;
                    }
                    previous_time = time;
                }
            }

            new_individual.CalculateFitness(
                solver.Problem,
                solver.FitnessCalculator);

            return new_individual;
        }
    }
}